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
using System.Text.Json.Serialization;
using System.ClientModel.Primitives;
using System.IO;

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

            StringBuilder sb = new StringBuilder();
            await foreach (StreamingChatCompletionUpdate completionUpdate in completionUpdates)
            {
                if (completionUpdate.ContentUpdate.Count > 0)
                {
                    sb.Append(completionUpdate.ContentUpdate[0].Text);
                    yield return completionUpdate.ContentUpdate[0].Text;
                }
            }
            chatMessages.Add(ChatMessage.CreateAssistantMessage(sb.ToString()));
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
                yield return ModelReaderWriter.Read<ChatMessage>(BinaryData.FromObjectAsJson(jsonElement), ModelReaderWriterOptions.Json);
            }
        }
        public static BinaryData SerializeMessages(IEnumerable<ChatMessage> messages)
        {
            using MemoryStream stream = new();
            using Utf8JsonWriter writer = new(stream);

            writer.WriteStartArray();

            foreach (IJsonModel<ChatMessage> message in messages)
            {
                message.Write(writer, ModelReaderWriterOptions.Json);
            }

            writer.WriteEndArray();
            writer.Flush();

            return BinaryData.FromBytes(stream.ToArray());
        }
        public void ClearChat()
        {
            chatMessages.Clear();
        }
    }
}
