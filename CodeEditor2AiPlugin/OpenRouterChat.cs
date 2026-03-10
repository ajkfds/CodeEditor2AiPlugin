using Avalonia.Collections;
using Avalonia.Threading;
using DynamicData;
using FaissNet;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ML;
using Microsoft.ML.Data;
using OpenAI.Realtime;
using Svg;
using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
//using OpenAI.Chat;
using System.Threading;
using System.Threading.Tasks;
using static CodeEditor2.CodeEditor.CodeDocument;
using static pluginAi.OpenRouterModels;
//using OpenAI;

namespace pluginAi
{
    public class OpenRouterChat: CodeEditor2.LLM.ILLMChatFrontEnd
    {

        public OpenRouterChat(OpenRouterModels.Model model, bool enableFunctionCalling)
        {
            initialize(model,enableFunctionCalling);
            this.enableFunctionCalling = enableFunctionCalling;
        }

        private Microsoft.Extensions.AI.IChatClient client;

        public static string? ApiKey;

        private bool enableFunctionCalling;
        private void initialize(OpenRouterModels.Model model,bool enableFunctionCalling)
        {
            // create OpenAI.Chat.ChatClient using OpenAi.net
            //string apiKey;
            //using (System.IO.StreamReader sw = new System.IO.StreamReader(@"C:\ApiKey\openrouter.txt"))
            //{
            //    apiKey = sw.ReadToEnd().Trim();
            //    if (apiKey == "") throw new Exception();
            //}
            if(ApiKey == null)
            {
                CodeEditor2.Controller.AppendLog("Set API Key for OpenRouter",Avalonia.Media.Colors.Red);
                return;
            }

            OpenAI.OpenAIClientOptions openAIClientOptions = new OpenAI.OpenAIClientOptions()
            {
                Endpoint = new System.Uri(
                    "https://openrouter.ai/api/v1"  // openrouter endpoint
                )
            };

            OpenAI.Chat.ChatClient openAiClient = new OpenAI.Chat.ChatClient(
                model: model.Name,
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
        }


        public Task SetModelAsync(OpenRouterModels.Model model,bool enableFunctionCalling)
        {
            initialize(model, enableFunctionCalling);
            return Task.CompletedTask;
        }

        List<Microsoft.Extensions.AI.ChatMessage> chatMessages = new List<Microsoft.Extensions.AI.ChatMessage>();

        public Task ResetAsync()
        {
            chatMessages.Clear();
            return Task.CompletedTask;
        }
        public async IAsyncEnumerable<string> GetAsyncCollectionChatResult(string command,IList<AITool>? tools, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            ChatOptions options = new ChatOptions();
            if (tools != null && enableFunctionCalling)
            {
                options = new()
                {
                    Tools = tools
                };
            }
            chatMessages.Add(new(ChatRole.User, command));

            List<ChatResponseUpdate> updates = [];

            await foreach (ChatResponseUpdate update in client.GetStreamingResponseAsync(chatMessages, options))
            {
                //foreach (var content in update.Contents)
                //{
                //    // A. 髢｢謨ｰ蜻ｼ縺ｳ蜃ｺ縺励・逋ｺ逕溘ｒ讀懃衍
                //    if (content is FunctionCallContent call)
                //    {
                //        Console.WriteLine($"\n[Function Call] {call.Name} 縺悟他縺ｳ蜃ｺ縺輔ｌ縺ｾ縺励◆縲ょｼ墓焚: {call.Arguments}");
                //    }

                //    // B. 髢｢謨ｰ縺ｮ螳溯｡檎ｵ先棡繧呈､懃衍
                //    if (content is FunctionResultContent result)
                //    {
                //        Console.WriteLine($"\n[Function Result] 邨先棡縺梧綾繧翫∪縺励◆: {result.Result}");
                //    }

                //    // C. 騾壼ｸｸ縺ｮ繝・く繧ｹ繝亥屓遲費ｼ磯先ｬ｡陦ｨ遉ｺ・・
                //    if (content is TextContent text)
                //    {
                //        Console.Write(text.Text);
                //    }
                //}
                yield return update.Text;
                updates.Add(update);
            }

            chatMessages.AddMessages(updates);
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

        public List<ChatMessage> GetChatMessages()
        {
            return chatMessages;
        }

        public async Task SaveMessagesAsync( string filePath)
        {
            try
            {

                string serializedData = SerializeMessages(chatMessages);
                await using var fs = File.OpenWrite(filePath);
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
            chatMessages.Clear();
            try
            {
                await using var fs = File.OpenRead(filePath); 
                using var sr = new StreamReader(fs); 
                string text = await sr.ReadToEndAsync(); 
                chatMessages = DeserializeMessages(BinaryData.FromString(text)).ToList();
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
        public static string SerializeMessages(IEnumerable<ChatMessage> messages)
        {
            return JsonSerializer.Serialize(messages);
        }
    }
}
