using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.AI;
using OpenAI.Assistants;
using OpenAI.Chat;

namespace pluginAi
{
    /// <summary>
    /// OpenRouter を内部で OpenAI.ChatClient (2.x) 経由で呼び出す IChatClient 実装
    /// </summary>
    public sealed class OpenRouterChatCompletionClient : IChatClient
    {
        private readonly OpenAI.Chat.ChatClient _innerClient;
        private readonly string _modelId;
        private readonly string? _applicationName;

        public OpenRouterChatCompletionClient(
            string apiKey,
            string modelId,
            string? applicationName = null
            )
        {
            _modelId = modelId ?? throw new ArgumentNullException(nameof(modelId));
            _applicationName = applicationName;

            var options = new OpenAI.OpenAIClientOptions
            {
                Endpoint = new Uri("https://openrouter.ai/api/v1"),
                UserAgentApplicationId = _applicationName
            };
            //client = new OpenAI.Chat.ChatClient(
            //    model: model,
            //    new ApiKeyCredential(apiKey),
            //    openAIClientOptions
            //    );

            var credential = new ApiKeyCredential(apiKey);
            var openAiClient = new OpenAI.OpenAIClient(credential, options);
            _innerClient = openAiClient.GetChatClient(modelId);
        }

        public void Dispose()
        {
        //    // => _innerClient?.Dispose();
        }


        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceKey is null && serviceType?.IsInstanceOfType(this) == true ? this : null;

        public async Task<ChatResponse> GetResponseAsync(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var openAiMessages = messages.Select(ToOpenAiMessage).ToList();

            var chatOptions = new OpenAI.Chat.ChatCompletionOptions();
            CopyOptions(options, chatOptions);

            var resp = await _innerClient.CompleteChatAsync(
                openAiMessages,
                chatOptions,
                cancellationToken)
                .ConfigureAwait(false);

            return ChatResponseFromOpenAi(resp);
        }
        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var openAiMessages = messages.Select(ToOpenAiMessage).ToList();
            var chatOptions = new OpenAI.Chat.ChatCompletionOptions();
            CopyOptions(options, chatOptions);

            await foreach (var chunk in _innerClient.CompleteChatStreamingAsync(
                openAiMessages,
                chatOptions,
                cancellationToken).ConfigureAwait(false))
            {
                yield return StreamFromOpenAi(chunk);
            }
        }


        private static OpenAI.Chat.ChatMessage ToOpenAiMessage(
            Microsoft.Extensions.AI.ChatMessage m)
        {
            switch (m.Role.Value)
            {
                case "system":
                    return new OpenAI.Chat.SystemChatMessage(m.Text ?? string.Empty);
                case "user":
                    return new OpenAI.Chat.UserChatMessage(m.Text ?? string.Empty);
                case "assistant":
                    return new OpenAI.Chat.AssistantChatMessage(m.Text ?? string.Empty);
                case "tool":
                    return new OpenAI.Chat.ToolChatMessage(m.Text ?? string.Empty);
                default:
                    throw new NotSupportedException($"Unknown role {m.Role}");
            }
        }

        private static void CopyOptions(
            ChatOptions? msOptions,
            OpenAI.Chat.ChatCompletionOptions openAiOptions)
        {
            if (msOptions is null) return;

            //dst.Model = src.Model;
            //dst.StopSequences = src.StopSequences?.ToList();
            //dst.Seed = src.Seed;
            //dst.User = src.User;


            openAiOptions.Temperature = msOptions.Temperature;
            openAiOptions.FrequencyPenalty = msOptions.FrequencyPenalty;
            openAiOptions.PresencePenalty = msOptions.PresencePenalty;
            openAiOptions.MaxOutputTokenCount = msOptions.MaxOutputTokens;
            openAiOptions.TopP = (float?)(msOptions.TopP);


            foreach (var s in msOptions.StopSequences ?? Array.Empty<string>())
                openAiOptions.StopSequences.Add(s);

            if (msOptions.Tools != null)
            {
                foreach (var tool in msOptions.Tools)
                {
                    System.Diagnostics.Debug.Print(tool.Name);
                    OpenAI.Chat.ChatTool openAiTool = OpenAI.Chat.ChatTool.CreateFunctionTool(
                    functionName: tool.Name,
                    functionDescription: tool.Description
                    );
                    openAiOptions.Tools.Add(openAiTool);
                }
            }

            if (msOptions.ResponseFormat is ChatResponseFormatText text)
                openAiOptions.ResponseFormat = global::OpenAI.Chat.ChatResponseFormat.CreateTextFormat();

            if (msOptions.ResponseFormat is ChatResponseFormatJson json)
                openAiOptions.ResponseFormat = global::OpenAI.Chat.ChatResponseFormat.CreateJsonObjectFormat();
        }
        //private static ToolDefinition ConvertTool(FunctionToolDefinition msTool)
        //{
        //    return new ToolDefinition
        //    {
        //        Function = new FunctionDefinition
        //        {
        //            Name = msTool.Name,
        //            Description = msTool.Description,
        //            Parameters = ConvertParameters(msTool)
        //        }
        //    };
        //}

        //private static FunctionParameters ConvertParameters(FunctionToolDefinition msTool)
        //{
        //    if (msTool.Parameters is null)
        //        return new FunctionParameters();

        //    var json = JsonSerializer.Serialize(msTool.Parameters);
        //    var doc = JsonDocument.Parse(json);
        //    return new FunctionParameters { Schema = doc.RootElement.Clone() };
        //}

        private static ChatResponse ChatResponseFromOpenAi(
            OpenAI.Chat.ChatCompletion c)
        {
            var text = string.Concat(c.Content.Select(part => part.Text));
            return new ChatResponse(
                new Microsoft.Extensions.AI.ChatMessage(
                    Microsoft.Extensions.AI.ChatRole.Assistant,
                    text))
            {
                ConversationId = c.Id,
                CreatedAt = c.CreatedAt,
                FinishReason = c.FinishReason switch
                {
                    OpenAI.Chat.ChatFinishReason.Stop => Microsoft.Extensions.AI.ChatFinishReason.Stop,
                    OpenAI.Chat.ChatFinishReason.Length => Microsoft.Extensions.AI.ChatFinishReason.Length,
                    OpenAI.Chat.ChatFinishReason.ToolCalls => Microsoft.Extensions.AI.ChatFinishReason.ToolCalls,
                    OpenAI.Chat.ChatFinishReason.ContentFilter => Microsoft.Extensions.AI.ChatFinishReason.ContentFilter,
                    _ => Microsoft.Extensions.AI.ChatFinishReason.Stop
                },
                Usage = new UsageDetails
                {
                    InputTokenCount = c.Usage?.InputTokenCount ?? 0,
                    OutputTokenCount = c.Usage?.OutputTokenCount ?? 0,
                    TotalTokenCount = c.Usage?.TotalTokenCount ?? 0
                },
                ModelId = c.Model
            };
        }

        private static Microsoft.Extensions.AI.ChatResponseUpdate StreamFromOpenAi(
            OpenAI.Chat.StreamingChatCompletionUpdate c)
        {
            if(c.ToolCallUpdates != null)
            {
                foreach(var update in c.ToolCallUpdates)
                {
                    update.
                }
            }

            ChatRole? role = null;
            switch (c.Role)
            {
                case ChatMessageRole.Assistant:
                    role = new ChatRole("assistant");
                    break;
                case ChatMessageRole.User:
                    role = new ChatRole("user");
                    break;
                case ChatMessageRole.Developer:
                    role = new ChatRole("developer");
                    break;
                case ChatMessageRole.Function:
                    role = new ChatRole("function");
                    break;
                case ChatMessageRole.System:
                    role = new ChatRole("System");
                    break;
                case ChatMessageRole.Tool:
                    role = new ChatRole("tool");
                    break;
            }

            return new Microsoft.Extensions.AI.ChatResponseUpdate(role, string.Concat(c.ContentUpdate.Select(part => part.Text)))
            {
                ConversationId = c.CompletionId,
                MessageId = c.CompletionId,
                CreatedAt = c.CreatedAt,
                FinishReason = c.FinishReason switch
                {
                    OpenAI.Chat.ChatFinishReason.Stop => Microsoft.Extensions.AI.ChatFinishReason.Stop,
                    OpenAI.Chat.ChatFinishReason.Length => Microsoft.Extensions.AI.ChatFinishReason.Length,
                    OpenAI.Chat.ChatFinishReason.ToolCalls => Microsoft.Extensions.AI.ChatFinishReason.ToolCalls,
                    OpenAI.Chat.ChatFinishReason.ContentFilter => Microsoft.Extensions.AI.ChatFinishReason.ContentFilter,
                    _ => Microsoft.Extensions.AI.ChatFinishReason.Stop
                },
                ModelId = c.Model
            };
        }



    }
}
