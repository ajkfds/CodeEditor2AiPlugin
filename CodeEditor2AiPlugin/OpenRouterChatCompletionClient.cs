using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.AI;
using OpenAI.Chat;

//using OpenAI;
//using OpenAI.Chat;

namespace pluginAi
{
    /// <summary>
    /// OpenRouter を内部で OpenAI.ChatClient (2.x) 経由で呼び出す IChatClient 実装
    /// </summary>
    public sealed class OpenRouterChatClient : IChatClient
    {
        private readonly OpenAI.Chat.ChatClient _innerClient;
        private readonly string _modelId;
        private readonly string? _applicationName;

        public OpenRouterChatClient(
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
            var chatOptions = new ChatCompletionOptions();
            CopyOptions(options, chatOptions);

            await foreach (var chunk in _innerClient.CompleteChatStreamingAsync(
                openAiMessages,
                chatOptions,
                cancellationToken).ConfigureAwait(false))
            {
                yield return StreamFromOpenAi(chunk);
            }
        }

        //public ChatClientMetadata Metadata { get; } =
        //    new(//clientName: nameof(OpenRouterChatClient),
        //        providerName: "OpenRouter"//,
        //        );// modelId: string.Empty); // modelId はセット時に毎回確認するのでここでは空


        //public async Task<ChatCompletion> CompleteAsync(
        //    IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
        //    ChatOptions? options = null,
        //    CancellationToken cancellationToken = default)
        //{
        //    var openAiMessages = messages.Select(ToOpenAiMessage).ToList();

        //    var chatOptions = new ChatCompletionOptions();
        //    CopyOptions(options, chatOptions);

        //    var resp = await _innerClient.CompleteChatAsync(
        //        openAiMessages,
        //        chatOptions,
        //        cancellationToken)
        //        .ConfigureAwait(false);

        //    return FromOpenAi(resp);
        //}

        //public async IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(
        //    IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
        //    ChatOptions? options = null,
        //    [EnumeratorCancellation] CancellationToken cancellationToken = default)
        //{
        //    var openAiMessages = messages.Select(ToOpenAiMessage).ToList();
        //    var chatOptions = new ChatCompletionOptions();
        //    CopyOptions(options, chatOptions);

        //    await foreach (var chunk in _innerClient.CompleteChatStreamingAsync(
        //        openAiMessages,
        //        chatOptions,
        //        cancellationToken).ConfigureAwait(false))
        //    {
        //        yield return FromOpenAi(chunk);
        //    }
        //}


        #region helpers

        private static OpenAI.Chat.ChatMessage ToOpenAiMessage(
            Microsoft.Extensions.AI.ChatMessage m)
        {
            switch (m.Role.Value)
            {
                case "system":
                    return new SystemChatMessage(m.Text ?? string.Empty);
                case "user":
                    return new UserChatMessage(m.Text ?? string.Empty);
                case "assistant":
                    return new AssistantChatMessage(m.Text ?? string.Empty);
                case "tool":
                    return new ToolChatMessage(m.Text ?? string.Empty);
                default:
                    throw new NotSupportedException($"Unknown role {m.Role}");
            }
        }

        private static void CopyOptions(
            ChatOptions? src,
            OpenAI.Chat.ChatCompletionOptions dst)
        {
            if (src is null) return;

            dst.Temperature = (float?)(src.Temperature);
            dst.MaxOutputTokenCount = src.MaxOutputTokens;
            dst.TopP = (float?)(src.TopP);
            foreach (var s in src.StopSequences ?? Array.Empty<string>())
                dst.StopSequences.Add(s);

//            if (src.Seed.HasValue) dst.Seed = (long)src.Seed.Value;

            if (src.ResponseFormat is ChatResponseFormatText text)
                dst.ResponseFormat = global::OpenAI.Chat.ChatResponseFormat.CreateTextFormat();

            if (src.ResponseFormat is ChatResponseFormatJson json)
                dst.ResponseFormat = global::OpenAI.Chat.ChatResponseFormat.CreateJsonObjectFormat();
        }


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
            return new Microsoft.Extensions.AI.ChatResponseUpdate(null, string.Concat(c.ContentUpdate.Select(part => part.Text)))
            {
                ConversationId = c.CompletionId,
                CreatedAt = c.CreatedAt,
                FinishReason = c.FinishReason switch
                {
                    OpenAI.Chat.ChatFinishReason.Stop => Microsoft.Extensions.AI.ChatFinishReason.Stop,
                    OpenAI.Chat.ChatFinishReason.Length => Microsoft.Extensions.AI.ChatFinishReason.Length,
                    OpenAI.Chat.ChatFinishReason.ToolCalls => Microsoft.Extensions.AI.ChatFinishReason.ToolCalls,
                    OpenAI.Chat.ChatFinishReason.ContentFilter => Microsoft.Extensions.AI.ChatFinishReason.ContentFilter,
                    _ => Microsoft.Extensions.AI.ChatFinishReason.Stop
                },
                Role = Microsoft.Extensions.AI.ChatRole.Assistant,
//                TextUpdate = string.Concat(c.ContentUpdate.Select(part => part.Text)),
                ModelId = c.Model
            };
        }



        #endregion
    }
}
