using DynamicData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ClientModel;
//using OpenAI.Chat;
using System.Threading;
using System.Runtime.CompilerServices;
using Svg;
using Avalonia.Threading;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.ClientModel.Primitives;
using System.IO;
using Microsoft.ML;
using Microsoft.ML.Data;
using System.Runtime.InteropServices;
using FaissNet;
using Microsoft.Extensions.AI;
using static CodeEditor2.CodeEditor.CodeDocument;
using Microsoft.Extensions.DependencyInjection;
//using OpenAI;

namespace pluginAi
{
    public class OpenRouterChat: ILLMChat
    {
        private Microsoft.Extensions.AI.IChatClient client;
        public OpenRouterChat(string model)
        {
            // create OpenAI.Chat.ChatClient using OpenAi.net
            string apiKey;
            using (System.IO.StreamReader sw = new System.IO.StreamReader(@"C:\ApiKey\openrouter.txt"))
            {
                apiKey = sw.ReadToEnd().Trim();
                if (apiKey == "") throw new Exception();
            }

            OpenAI.OpenAIClientOptions openAIClientOptions = new OpenAI.OpenAIClientOptions()
            {
                Endpoint = new System.Uri(
                    "https://openrouter.ai/api/v1"  // openrouter endpoint
                )
            };

            OpenAI.Chat.ChatClient openAiClient = new OpenAI.Chat.ChatClient(
                model: model,
                new ApiKeyCredential(apiKey),
                openAIClientOptions
                );

            // create IChatClient with OpenAI.Chat.ChatClient
            client = Microsoft.Extensions.AI.OpenAIClientExtensions.AsIChatClient(openAiClient);

            //use user function
            client = ChatClientBuilderChatClientExtensions
               .AsBuilder(client)
               .UseFunctionInvocation()
               .Build();
        }

        string GetCurrentWeather(string place)
        {
            return Random.Shared.NextDouble() > 0.5 ? "It's sunny" : "It's raining";
        }


        List<Microsoft.Extensions.AI.ChatMessage> chatMessages = new List<Microsoft.Extensions.AI.ChatMessage>();

        public async IAsyncEnumerable<string> GetAsyncCollectionChatResult(string command, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            ChatOptions options = new() { Tools = [
                AIFunctionFactory.Create(GetCurrentWeather)] };
            chatMessages.Add(new(ChatRole.User, command));

            List<ChatResponseUpdate> updates = [];
            await foreach (ChatResponseUpdate update in
                client.GetStreamingResponseAsync(chatMessages,options))
            {
                yield return update.Text;
                updates.Add(update);
            }
            chatMessages.AddMessages(updates);
        }
        public async Task<string> GetAsyncChatResult(string command, CancellationToken cancellationToken)
        {
            StringBuilder sb = new StringBuilder();
            await foreach (string ret in GetAsyncCollectionChatResult(command, cancellationToken))
            {
                sb.Append(ret);
            }
            return sb.ToString();
        }

        public List<ChatMessage> GetChatMessages()
        {
            return chatMessages;
        }

        public void SaveMessages( string filePath)
        {
            try
            {
                BinaryData serializedData = SerializeMessages(chatMessages);
                using (System.IO.StreamWriter sw = new System.IO.StreamWriter(filePath))
                {
                    sw.Write(serializedData);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Save error: {ex.Message}");
            }
        }

        
        public void LoadMessages(string filePath)
        {
            chatMessages.Clear();
            try
            { 
                using(System.IO.StreamReader sr = new System.IO.StreamReader(filePath))
                {
                    chatMessages = DeserializeMessages(BinaryData.FromString(sr.ReadToEnd())).ToList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Load error: {ex.Message}");
            }
        }

        public static IEnumerable<ChatMessage> DeserializeMessages(BinaryData data)
        {
            using JsonDocument messagesAsJson = JsonDocument.Parse(data.ToMemory());

            foreach (JsonElement jsonElement in messagesAsJson.RootElement.EnumerateArray())
            {
                var message = JsonSerializer.Deserialize<ChatMessage>(jsonElement.GetRawText());
                if (message != null)
                {
                    yield return message;
                }
            }

        }
        public static BinaryData SerializeMessages(IEnumerable<ChatMessage> messages)
        {
            return BinaryData.FromString(JsonSerializer.Serialize(messages));
        }
        public void ClearChat()
        {
            chatMessages.Clear();
        }
    }
}
