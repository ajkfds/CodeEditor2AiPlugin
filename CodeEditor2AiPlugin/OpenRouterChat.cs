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
using System.Text.Json;

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
        List<ChatMessage> chatMessages = new List<ChatMessage>();

        public async IAsyncEnumerable<string> GetAsyncCollectionChatResult(string command, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
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
            chatMessages.Add(ChatMessage.CreateAssistantMessage(sb.ToString()));
            return sb.ToString();
        }

        public void SaveMessages( string filePath)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                System.IO.File.WriteAllText(filePath, JsonSerializer.Serialize(chatMessages, options));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Save error: {ex.Message}");
            }
        }

        public void LoadMessages(string filePath)
        {
            try
            {
                if (!System.IO.File.Exists(filePath)) return;

                var json = System.IO.File.ReadAllText(filePath);
                List<ChatMessage>? messages = JsonSerializer.Deserialize<List<ChatMessage>>(json);
                if(messages == null) return;
                chatMessages = messages;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Load error: {ex.Message}");
            }
        }

        public void ClearChat()
        {
            chatMessages.Clear();
        }

    }
}
