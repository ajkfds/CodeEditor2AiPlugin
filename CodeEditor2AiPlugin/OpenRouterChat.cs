using DynamicData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ClientModel;
using OpenAI.Chat;
using System.Threading;
using System.Runtime.CompilerServices;
using Svg;
using Avalonia.Threading;

namespace CodeEditor2AiPlugin
{
    public class OpenRouterChat: ILLMChat
    {
        public OpenRouterChat()
        {
            using (System.IO.StreamReader sw = new System.IO.StreamReader(@"C:\ApiKey\openrouter.txt"))
            {
                apiKey = sw.ReadToEnd().Trim();
                if (apiKey == "") throw new Exception();
            }
            OpenAI.OpenAIClientOptions openAIClientOptions = new OpenAI.OpenAIClientOptions()
            { Endpoint = new System.Uri("https://openrouter.ai/api/v1") };

            client = new OpenAI.Chat.ChatClient(
                model: "deepseek/deepseek-r1:free",
                new ApiKeyCredential(apiKey),
                openAIClientOptions
                );
        }
        private string apiKey;
        private OpenAI.Chat.ChatClient client;

        public async IAsyncEnumerable<string> GetAsyncCollectionChatResult(string command, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            List<ChatMessage> chatMessages = new List<ChatMessage>();
            chatMessages.Add(ChatMessage.CreateUserMessage(command));

            AsyncCollectionResult<StreamingChatCompletionUpdate> completionUpdates
                = client.CompleteChatStreamingAsync(chatMessages, null, cancellationToken);

            await foreach (StreamingChatCompletionUpdate completionUpdate in completionUpdates)
            {
                if (completionUpdate.ContentUpdate.Count > 0)
                {
                    yield return completionUpdate.ContentUpdate[0].Text;
                }
            }
        }

        public async Task<string> GetAsyncChatResult(string command,CancellationToken cancellationToken)
        {
            StringBuilder sb = new StringBuilder();
            await foreach (string ret in GetAsyncCollectionChatResult(command, cancellationToken))
            {
                sb.Append(ret);
            }
            return sb.ToString();
        }

    }
}
