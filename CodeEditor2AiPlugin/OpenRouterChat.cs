using DynamicData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ClientModel;
using OpenAI.Chat;

namespace CodeEditor2AiPlugin
{
    public class OpenRouterChat
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

        public async IAsyncEnumerable<string> GetAsyncCollectionChatResult(string command)
        {
            AsyncCollectionResult<StreamingChatCompletionUpdate> completionUpdates
                = client.CompleteChatStreamingAsync(command);

            await foreach (StreamingChatCompletionUpdate completionUpdate in completionUpdates)
            {
                if (completionUpdate.ContentUpdate.Count > 0)
                {
                    yield return completionUpdate.ContentUpdate[0].Text;
                }
            }
        }

    }
}
