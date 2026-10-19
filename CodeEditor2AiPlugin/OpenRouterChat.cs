using Microsoft.Extensions.AI;
using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ModelItem = CodeEditor2.LLM.ModelItem;

namespace pluginAi
{
    public class OpenRouterChat : CodeEditor2.LLM.ILLMChatFrontEnd
    {

        public OpenRouterChat(string modelName, bool enableFunctionCalling, bool includeReasoning = false)
        {
            initialize(modelName, enableFunctionCalling);
            this.EnableFunctionCalling = enableFunctionCalling;
            this.IncludeReasoning = includeReasoning;
        }

        private Microsoft.Extensions.AI.IChatClient client;

        public static string? ApiKey;

        public bool EnableFunctionCalling { get; }
        public bool IncludeReasoning { get; set; }

        /// <summary>
        /// OpenRouter provider routing の "only" に指定する provider slug (例: "gmicloud/fp8")。
        /// null または空なら routing 制限なし。
        /// OpenRouter 固有の非標準フィールドのため、MEAI アダプタを経由せず
        /// HttpClient パイプライン (ProviderRoutingHandler) でリクエストボディに注入する。
        /// </summary>
        public string? ProviderOnly { get; set; }

        private string? currentModelName = null;
        private ModelItem? currentModel = null;

        /// <summary>
        /// The currently selected model (null when not yet initialized)
        /// </summary>
        public ModelItem? CurrentModel => currentModel;

        private string? lastUsageInfo = null;

        /// <summary>
        /// Human-readable summary of the token usage of the most recent LLM
        /// response (e.g. "tokens: 1234 in / 567 out / 1801 total").
        /// null when the provider did not report usage.
        /// Display-only; never part of the message content sent to the LLM.
        /// </summary>
        public string? LastUsageInfo => lastUsageInfo;

        private void initialize(string modelName, bool enableFunctionCalling)
        {

            if (ApiKey == null)
            {
                CodeEditor2.Controller.AppendLog("Set API Key for OpenRouter", Avalonia.Media.Colors.Red);
                return;
            }

            OpenAI.OpenAIClientOptions openAIClientOptions = new OpenAI.OpenAIClientOptions()
            {
                Endpoint = new System.Uri(
                    "https://openrouter.ai/api/v1"  // openrouter endpoint
                )
            };

            // MEAI OpenAI adapter は ChatOptions.AdditionalProperties を OpenAI SDK の
            // リクエストボディに転送しないため、OpenRouter 固有の "provider" フィールドは
            // この経路では送れない。そこで HttpClient に DelegatingHandler を挟み、
            // /chat/completions へのリクエストボディを送信直前に書き換えて
            // provider.only / allow_fallbacks を注入する。
            // (ProviderOnly は set 可能プロパティのため、ラムダ経由で最新値を参照する)
            openAIClientOptions.Transport = new HttpClientPipelineTransport(
                new HttpClient(new ProviderRoutingHandler(() => ProviderOnly, new HttpClientHandler())));

            OpenAI.Chat.ChatClient openAiClient = new OpenAI.Chat.ChatClient(
                model: modelName,
                new ApiKeyCredential(ApiKey),
                openAIClientOptions
                );

            // create IChatClient with OpenAI.Chat.ChatClient
            client = Microsoft.Extensions.AI.OpenAIClientExtensions.AsIChatClient(openAiClient);

            //use user function
            if (enableFunctionCalling)
            {
                client = ChatClientBuilderChatClientExtensions
                   .AsBuilder(client)
                   .UseFunctionInvocation()
                   .Build();
            }
            else
            {
                client = ChatClientBuilderChatClientExtensions
                   .AsBuilder(client)
                   .Build();
            }

            // record current model only after successful client initialization
            currentModelName = modelName;

            // reflect the current model so UI (ChatControl model selector) can show it
            OpenRouterModels.Model? matched = OpenRouterModels.GetAllModels().FirstOrDefault(m => m.Name == modelName);
            if (matched != null)
            {
                currentModel = new ModelItem { Id = matched.Name, Name = matched.Caption, Tag = matched };
            }
        }


        public Task SetModelAsync(string modelName, bool enableFunctionCalling)
        {
            initialize(modelName, enableFunctionCalling);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets the list of available models
        /// </summary>
        public List<ModelItem> GetAvailableModels()
        {
            // dynamically generated from the OpenRouterModels static model list
            return OpenRouterModels.GetAllModels()
                .Select(model => new ModelItem { Id = model.Name, Name = model.Caption, Tag = model })
                .ToList();
        }

        /// <summary>
        /// Sets the current model from a ModelItem
        /// </summary>
        public Task SetModelAsync(ModelItem modelItem)
        {
            if (modelItem.Tag is OpenRouterModels.Model model)
            {
                initialize(model.Name, EnableFunctionCalling);
                currentModel = modelItem;
            }
            return Task.CompletedTask;
        }

        public List<CodeEditor2.LLM.ChatMessageWrapper> ChatMessageWrappers { get; } = new List<CodeEditor2.LLM.ChatMessageWrapper>();

        public Task ResetAsync()
        {
            ChatMessageWrappers.Clear();
            return Task.CompletedTask;
        }

        public Task<bool> TryReconnectAsync()
        {
            if (currentModelName == null) return Task.FromResult(false);

            try
            {
                // Reinitialize the client
                // Get the current model from the last message if available
//                OpenRouterModels.Model model = OpenRouterModels.deepseek_deepseek_v3_2;
                initialize(currentModelName, EnableFunctionCalling);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                CodeEditor2.Controller.AppendLog($"Reconnection failed: {ex.Message}", Avalonia.Media.Colors.Red);
                return Task.FromResult(false);
            }
        }

        public void RemoveLastUserMessage()
        {
            // Remove the last user message from the history
            for (int i = ChatMessageWrappers.Count - 1; i >= 0; i--)
            {
                if (ChatMessageWrappers[i].Role == ChatRole.User)
                {
                    ChatMessageWrappers.RemoveAt(i);
                    break;
                }
            }
        }
        public async IAsyncEnumerable<string> GetAsyncCollectionChatResult(string command, IList<AITool>? tools, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            ChatOptions options = new ChatOptions
            {
                Temperature = 1.0f,
                TopP = 0.95f,
                Reasoning = new ReasoningOptions() { Effort = ReasoningEffort.Low }
            };
            //ChatOptions options = new ChatOptions
            //{
            //    // MiniMax M2.5 推奨値: 1.0 (0.0にすると思考がスタックして空文字になりやすい)
            //    Temperature = 1.0f,
            //    TopP = 0.95f,
            //};

            //            ChatOptions options = new ChatOptions();
            if (tools != null && EnableFunctionCalling)
            {
                options = new()
                {
                    Tools = tools
                };
            }
            if (IncludeReasoning)
            {
                options.AdditionalProperties = new() { ["include_reasoning"] = true };
            }
            // OpenRouter provider routing は ProviderRoutingHandler が
            // リクエストボディに直接注入する (AdditionalProperties は
            // MEAI OpenAI アダプタで転送されないため使えない)
            ChatMessageWrappers.Add(new(ChatRole.User, command));

            List<ChatResponseUpdate> updates = new List<ChatResponseUpdate>();

            // Stream chunks to the caller immediately (no full buffering).
            // This keeps the ChatControl "waiting..." timer stoppable and makes
            // the Abort button effective: the cancellationToken is propagated to
            // the underlying stream, so Cancel() throws OperationCanceledException
            // here and completeWork can unwind (restoring inputAcceptable).
            try
            {
                await foreach (ChatResponseUpdate update in client.GetStreamingResponseAsync(ChatMessageWrappers, options, cancellationToken))
                {
                    // 全ての update を収集する。
                    // 最終チャンクのように Text が空で usage (UsageContent) のみを
                    // 運ぶ update や、FunctionCallContent を運ぶ update を
                    // 取りこぼすと、以降の updates.ToChatResponse() で usage 情報や
                    // tool call 履歴が欠落する (lastUsageInfo が null になる / 次ターンで
                    // assistant tool-call メッセージが消失する) ため、
                    // 条件で絞らず全件を蓄積する。
                    updates.Add(update);

                    // FinishReason が入っていれば通知
                    if (update.FinishReason.HasValue)
                    {
                        ChatFinishReason reason = update.FinishReason.Value;

                        if (reason == ChatFinishReason.ToolCalls)
                        {
                            yield return "blank (Function Call requested)";
                        }
                        else if (reason == ChatFinishReason.ContentFilter)
                        {
                            yield return "blank (Content filtered)";
                        }
                        else if (reason == ChatFinishReason.Length)
                        {
                            yield return "blank (Max tokens exceeded)";
                        }
                        else if (reason != ChatFinishReason.Stop)
                        {
                            // Value プロパティで文字列値（"tool_calls", "stop" など）を取得できます
                            yield return $"blank ({reason.Value})";
                        }
                    }

                    // テキストチャンクが含まれている場合は即座に yield
                    // (収集はループ先頭で実施済み)
                    if (!string.IsNullOrEmpty(update.Text))
                    {
                        yield return update.Text;
                    }
                }
            }
            finally
            {
                // 履歴は正常終了・キャンセル・例外のどの経路でも反映する
                addMessages(updates);
            }
        }


        private void addMessages(IEnumerable<ChatResponseUpdate> updates)
        {

            if (updates is ICollection<ChatResponseUpdate> { Count: 0 })
            {
                lastUsageInfo = null;
                return;
            }

            ChatResponse response = updates.ToChatResponse();

            // Capture token usage of this response (when the provider reports it)
            // so the UI can show it as an informational footer line.
            lastUsageInfo = FormatUsage(response.Usage);

            foreach (var message in response.Messages)
            {
                CodeEditor2.LLM.ChatMessageWrapper wrapper = new CodeEditor2.LLM.ChatMessageWrapper(message);
                // Attach the usage info as an informational footer line and
                // persist it in AdditionalProperties so it survives Save/Load.
                // It is display-only and never becomes part of the message text.
                if (lastUsageInfo != null)
                {
                    wrapper.InfoMessage = lastUsageInfo;
                    wrapper.AdditionalProperties ??= new AdditionalPropertiesDictionary();
                    wrapper.AdditionalProperties["infoMessage"] = lastUsageInfo;
                }
                ChatMessageWrappers.Add(wrapper);
            }
        }

        /// <summary>
        /// Format an <see cref="UsageDetails"/> into a compact human-readable
        /// string, e.g. "tokens: 1234 in / 567 out / 1801 total".
        /// Returns null when no usage information is available.
        /// </summary>
        private static string? FormatUsage(UsageDetails? usage)
        {
            if (usage == null) return null;

            long? input = usage.InputTokenCount;
            long? output = usage.OutputTokenCount;
            long? cashed = usage.CachedInputTokenCount;
            long? total = usage.TotalTokenCount;

            if (input == null && output == null && total == null) return null;

            StringBuilder sb = new StringBuilder("tokens:");
            if (input != null) sb.Append(' ').Append(input.Value).Append(" in");
            if (cashed != null) sb.Append(' ').Append(cashed.Value).Append(" cashed");
            if (output != null) sb.Append(" / ").Append(output.Value).Append(" out");
//            if (total != null) sb.Append(" / ").Append(total.Value).Append(" total");
            return sb.ToString();
        }



        public async Task<string> GetAsyncChatResult(string command, IList<AITool>? tools, CancellationToken cancellationToken)
        {
            StringBuilder sb = new StringBuilder();
            await foreach (string ret in GetAsyncCollectionChatResult(command, tools, cancellationToken))
            {
                sb.Append(ret);
            }
            return sb.ToString();
        }


        public async Task SaveMessagesAsync(string filePath)
        {
            try
            {

                string serializedData = SerializeMessages(ChatMessageWrappers);
                await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                using var sw = new StreamWriter(fs);
                await sw.WriteAsync(serializedData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Save error: {ex.Message}");
            }
        }


        public async Task LoadMessagesAsync(string filePath)
        {
            ChatMessageWrappers.Clear();
            try
            {
                await using var fs = File.OpenRead(filePath);
                using var sr = new StreamReader(fs);
                string text = await sr.ReadToEndAsync();
                List<Microsoft.Extensions.AI.ChatMessage> messages = DeserializeMessages(BinaryData.FromString(text)).ToList();
                
                // Add deserialized messages to ChatMessageWrappers
                foreach (var message in messages)
                {
                    ChatMessageWrappers.Add(new CodeEditor2.LLM.ChatMessageWrapper(message));
                }
            }
            catch (System.InvalidOperationException ex) when (ex.Message.Contains("String") && ex.Message.Contains("Number"))
            {
                CodeEditor2.Controller.AppendLog($"JSON parsing error while loading: {ex.Message}", Avalonia.Media.Colors.Red);
            }
            catch (Exception ex)
            {
                CodeEditor2.Controller.AppendLog($"Load error: {ex.Message}", Avalonia.Media.Colors.Red);
            }
        }

        private static JsonSerializerOptions GetSerializerOptions()
        {
            // Use default options with IncludeFields to properly serialize/deserialize ChatMessage
            return new JsonSerializerOptions
            {
                IncludeFields = true,
                PropertyNameCaseInsensitive = true
            };
        }

        /// <summary>
        /// /chat/completions リクエストボディに OpenRouter の provider routing
        /// (only / allow_fallbacks) を注入する DelegatingHandler。
        /// </summary>
        private sealed class ProviderRoutingHandler : DelegatingHandler
        {
            private readonly Func<string?> _providerOnlyGetter;

            public ProviderRoutingHandler(Func<string?> providerOnlyGetter, HttpMessageHandler innerHandler)
                : base(innerHandler)
            {
                _providerOnlyGetter = providerOnlyGetter;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (request.RequestUri != null &&
                    request.RequestUri.AbsolutePath.EndsWith("/chat/completions", StringComparison.Ordinal) &&
                    request.Content != null)
                {
                    string? providerOnly = _providerOnlyGetter();
                    if (!string.IsNullOrEmpty(providerOnly))
                    {
                        byte[] body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
                        JsonNode? root = JsonNode.Parse(body);
                        if (root is JsonObject obj && !obj.ContainsKey("provider"))
                        {
                            obj["provider"] = new JsonObject
                            {
                                ["only"] = new JsonArray(providerOnly),
                                ["allow_fallbacks"] = false
                            };
                            byte[] newBody = Encoding.UTF8.GetBytes(obj.ToJsonString());
                            ByteArrayContent newContent = new ByteArrayContent(newBody);
                            newContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                            request.Content = newContent;
                        }
                    }
                }
                return await base.SendAsync(request, cancellationToken);
            }
        }

        public static List<ChatMessage> DeserializeMessages(BinaryData data)
        {
            List<ChatMessage> result = new List<ChatMessage>();
            using JsonDocument messagesAsJson = JsonDocument.Parse(data.ToMemory());

            var options = GetSerializerOptions();

            foreach (JsonElement jsonElement in messagesAsJson.RootElement.EnumerateArray())
            {
                try
                {
                    var message = JsonSerializer.Deserialize<ChatMessage>(jsonElement.GetRawText(), options);
                    if (message != null)
                    {
                        result.Add(message);
                    }
                }
                catch (System.InvalidOperationException ex) when (ex.Message.Contains("String") && ex.Message.Contains("Number"))
                {
                    // Skip malformed messages that have type mismatches (e.g., number instead of string)
                    CodeEditor2.Controller.AppendLog($"Skipping malformed message: {ex.Message}", Avalonia.Media.Colors.Orange);
                    continue;
                }
            }
            return result;
        }
        public static string SerializeMessages(IEnumerable<ChatMessage> messages)
        {
            return JsonSerializer.Serialize(messages, GetSerializerOptions());
        }
    }
}
