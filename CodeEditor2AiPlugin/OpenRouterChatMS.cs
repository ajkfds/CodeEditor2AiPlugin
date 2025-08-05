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

namespace pluginAi
{
    public class OpenRouterChatMS: ILLMChat
    {
        public OpenRouterChatMS(string model)
        {
            using (System.IO.StreamReader sw = new System.IO.StreamReader(@"C:\ApiKey\openrouter.txt"))
            {
                apiKey = sw.ReadToEnd().Trim();
                if (apiKey == "") throw new Exception();
            }
            client = new OpenRouterChatCompletionClient(apiKey: apiKey, model);

            client = ChatClientBuilderChatClientExtensions
                .AsBuilder(client)
                .UseFunctionInvocation()
                .Build();
        }
        string GetCurrentWeather() => Random.Shared.NextDouble() > 0.5 ? "It's sunny" : "It's raining";

        private string apiKey;
        private IChatClient client;
        List<ChatMessage> chatMessages = new List<ChatMessage>();

        public async IAsyncEnumerable<string> GetAsyncCollectionChatResult(string command, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            ChatOptions options = new() { Tools = [AIFunctionFactory.Create(GetCurrentWeather)] };
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

        //public class InputData
        //{
        //    [LoadColumn(0)]
        //    public string Text { get; set; }
        //}

        //public class OutputData
        //{
        //    [VectorType]
        //    public float[] Features { get; set; }
        //}



        //public class TextData
        //{
        //    public string Text { get; set; }
        //}

        // chat log control
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
