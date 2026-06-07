using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace pluginAi
{
    public class MeaiStatefulChatAgent : CodeEditor2.LLM.IStatefulChatAgent
    {
        private readonly IChatClient _chatClient;
        private readonly List<ChatMessage> _history = new();
        private readonly List<AITool> _tools = new();

        // ==========================================
        // イベント
        // ==========================================
        public event EventHandler<CodeEditor2.LLM.ToolExecutionEventArgs>? ToolExecutionStarted;
        public event EventHandler<CodeEditor2.LLM.ToolExecutionEventArgs>? ToolExecutionCompleted;

        // ==========================================
        // プロパティ
        // ==========================================
        public string ModelId { get; set; } = string.Empty;
        public ChatOptions DefaultOptions { get; set; } = new ChatOptions();
        public IReadOnlyList<ChatMessage> ChatHistory => _history.AsReadOnly();

        public MeaiStatefulChatAgent(IChatClient chatClient)
        {
            _chatClient = chatClient;
        }

        // ==========================================
        // 履歴・ツール管理
        // ==========================================
        public void LoadHistory(IEnumerable<ChatMessage> history)
        {
            _history.Clear();
            _history.AddRange(history);
        }

        public void ClearHistory() => _history.Clear();

        public void RegisterTool(AITool tool)
        {
            if (!_tools.Contains(tool)) _tools.Add(tool);
        }

        public void ClearTools() => _tools.Clear();

        // ==========================================
        // メッセージ送信（非ストリーミング）
        // ==========================================
        public async Task<ChatResponse> SendMessageAsync(
            string message,
            ChatOptions? overrideOptions = null,
            CancellationToken cancellationToken = default)
        {
            _history.Add(new ChatMessage(ChatRole.User, message));

            while (true)
            {
                var options = PrepareOptions(overrideOptions);

                // 変更点: GetResponseAsync を使用
                var response = await _chatClient.GetResponseAsync(_history, options, cancellationToken);

                // ChatResponse.Messages から assistant message を抽出して追加
                foreach (var assistantMsg in response.Messages)
                {
                    _history.Add(assistantMsg);
                }

                // 変更点: Contents から FunctionCallContent を抽出
                var toolCalls = response.Messages
                    .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
                    .ToList();

                if (toolCalls.Count == 0)
                {
                    return response; // ツール呼び出しがなければ終了
                }

                foreach (var toolCall in toolCalls)
                {
                    await ExecuteToolAndAddHistoryAsync(toolCall, cancellationToken);
                }
                // ツールの結果が _history に追加され、ループの先頭に戻る
            }
        }

        // ==========================================
        // メッセージ送信（ストリーミング）
        // ==========================================
        public async IAsyncEnumerable<ChatResponseUpdate> SendMessageStreamAsync(
            string message,
            ChatOptions? overrideOptions = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            _history.Add(new ChatMessage(ChatRole.User, message));

            while (true)
            {
                var options = PrepareOptions(overrideOptions);

                var toolCallUpdates = new List<FunctionCallContent>();
                var textMessageBuilder = new System.Text.StringBuilder();
                ChatRole? responseRole = null;

                // 変更点: GetStreamingResponseAsync を使用
                await foreach (var update in _chatClient.GetStreamingResponseAsync(_history, options, cancellationToken))
                {
                    if (update.Role.HasValue)
                    {
                        responseRole ??= update.Role;
                    }

                    // 変更点: Contents から FunctionCallContent の断片を抽出
                    var functionCalls = update.Contents.OfType<FunctionCallContent>().ToList();
                    if (functionCalls.Any())
                    {
                        toolCallUpdates.AddRange(functionCalls);
                    }

                    if (!string.IsNullOrEmpty(update.Text))
                    {
                        textMessageBuilder.Append(update.Text);
                        yield return update;
                    }
                }

                var assistantMessage = new ChatMessage(responseRole ?? ChatRole.Assistant, textMessageBuilder.ToString());

                // ツール呼び出しがあった場合の処理
                if (toolCallUpdates.Count > 0)
                {
                    var combinedCalls = CombineToolCallUpdates(toolCallUpdates);

                    // 結合したツール呼び出しをアシスタントメッセージにセット
                    foreach (var call in combinedCalls)
                    {
                        assistantMessage.Contents.Add(call);
                    }
                    _history.Add(assistantMessage);

                    foreach (var toolCall in combinedCalls)
                    {
                        await ExecuteToolAndAddHistoryAsync(toolCall, cancellationToken);
                    }
                    continue; // ループの先頭に戻り、結果を送信
                }

                _history.Add(assistantMessage);
                break;
            }
        }

        // ==========================================
        // 内部ヘルパーメソッド
        // ==========================================
        private ChatOptions PrepareOptions(ChatOptions? overrideOptions)
        {
            var options = overrideOptions ?? DefaultOptions.Clone();
            if (!string.IsNullOrEmpty(ModelId)) options.ModelId = ModelId;
            if (_tools.Count > 0) options.Tools = new List<AITool>(_tools);
            return options;
        }

        private async Task ExecuteToolAndAddHistoryAsync(FunctionCallContent toolCall, CancellationToken ct)
        {
            ToolExecutionStarted?.Invoke(this, new CodeEditor2.LLM.ToolExecutionEventArgs(toolCall.Name, toolCall.Arguments));

            string resultString;
            try
            {
                var functionTool = _tools.OfType<AIFunction>().FirstOrDefault(f => f.Name == toolCall.Name);

                if (functionTool != null)
                {
                    // IDictionary<string, object?> を AIFunctionArguments に変換
                    AIFunctionArguments args = new AIFunctionArguments();
                    if (toolCall.Arguments != null)
                    {
                        foreach (var kvp in toolCall.Arguments)
                        {
                            args.Add(kvp.Key, kvp.Value);
                        }
                    }
                    var result = await functionTool.InvokeAsync(args, ct);
                    resultString = result?.ToString() ?? "null";
                }
                else
                {
                    resultString = $"Error: Tool '{toolCall.Name}' is not registered.";
                }
            }
            catch (Exception ex)
            {
                resultString = $"Error executing tool: {ex.Message}";
            }

            ToolExecutionCompleted?.Invoke(this, new CodeEditor2.LLM.ToolExecutionEventArgs(toolCall.Name, toolCall.Arguments, resultString));

            // 変更点: FunctionResultContent を作成し、Contents に追加して履歴へ
            // 変更点: 第2引数（テキストコンテンツ）に null を渡し、
            // 代わりに FunctionResultContent を Contents に追加する
            var resultMessage = new ChatMessage(Microsoft.Extensions.AI.ChatRole.Tool, "");
            resultMessage.Contents.Add(new FunctionResultContent(toolCall.CallId, resultString));
            _history.Add(resultMessage);
        }

        private List<FunctionCallContent> CombineToolCallUpdates(List<FunctionCallContent> updates)
        {
            var grouped = updates.GroupBy(u => u.CallId);
            var result = new List<FunctionCallContent>();

            foreach (var group in grouped)
            {
                var callId = group.Key;
                var name = group.FirstOrDefault(u => !string.IsNullOrEmpty(u.Name))?.Name ?? "";

                var mergedArgs = new Dictionary<string, object?>();
                foreach (var update in group)
                {
                    if (update.Arguments != null)
                    {
                        foreach (var kvp in update.Arguments)
                        {
                            mergedArgs[kvp.Key] = kvp.Value;
                        }
                    }
                }

                result.Add(new FunctionCallContent(callId, name, mergedArgs));
            }

            return result;
        }
    }
}
