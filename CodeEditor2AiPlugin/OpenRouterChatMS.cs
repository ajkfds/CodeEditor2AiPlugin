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
        }

        private string apiKey;
        private OpenRouterChatCompletionClient client;
        List<ChatMessage> chatMessages = new List<ChatMessage>();

        public async IAsyncEnumerable<string> GetAsyncCollectionChatResult(string command, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            /*
            chatMessages.Add(ChatMessage.CreateUserMessage(command));
            GetVector(command);

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
             */
            chatMessages.Add(new(ChatRole.User, command));

            List<ChatResponseUpdate> updates = [];
            await foreach (ChatResponseUpdate update in
                client.GetStreamingResponseAsync(chatMessages))
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
        public class InputData
        {
            [LoadColumn(0)]
            public string Text { get; set; }
        }

        public class OutputData
        {
            [VectorType]
            public float[] Features { get; set; }
        }



        public class TextData
        {
            public string Text { get; set; }
        }

        // message vector search
        private IEnumerable<float[]> GetVector(string text)
        {
            // 1. MLコンテキストの作成
            var mlContext = new MLContext();

            // 2. サンプルデータの定義
            var data = new[]
            {
            new InputData { Text = text }
            };

            // 3. データをIDataViewに変換
            var dataView = mlContext.Data.LoadFromEnumerable(data);

            // 4. テキストのベクトル化プロセスを定義
            var textPipeline = mlContext.Transforms.Text.FeaturizeText(
                outputColumnName: "Features",
                inputColumnName: "Text");

            // 5. トランスフォーマーのトレーニング
            var transformer = textPipeline.Fit(dataView);

            // 6. データの変換
            var transformedData = transformer.Transform(dataView);

            // 7. 結果を取得
            var featuresColumn = mlContext.Data.CreateEnumerable<OutputData>(transformedData, reuseRowObject: false);
            IEnumerable<float[]> featureEnumerable = featuresColumn.Select(d => d.Features);

            return featureEnumerable;
        }
        public string GetRagText(string text, string path)
        {

            // MLContextの作成
            var mlContext = new MLContext();
            string[] filePathList = System.IO.Directory.GetFiles(path);

            List<TextData> texts = new List<TextData>();
            //var data = new[] { new { Text = "Hello world" }, new { Text = "Goodbye world" } };
            foreach (string filePath in filePathList)
            {
                using (System.IO.TextReader reader = new System.IO.StreamReader(filePath))
                {
                    string fileText = reader.ReadToEnd();
                    texts.Add(new TextData { Text = fileText });
                }
            }
            TextData[] dataset = texts.ToArray();
            // データセット（検索対象データ）
            //var dataset = new[]
            //{
            //    new TextData { Text = "I love programming" },
            //    new TextData { Text = "FaissNet makes vector search efficient" },
            //    new TextData { Text = "C# is a versatile language" },
            //    new TextData { Text = "Machine learning is fascinating" },
            //    new TextData { Text = "Coding challenges are fun" }
            //};

            // データをIDataViewに変換
            var dataView = mlContext.Data.LoadFromEnumerable(dataset);

            // テキストをTF-IDFベクトル化
            var pipeline = mlContext.Transforms.Text.FeaturizeText("Features", nameof(TextData.Text));
            var model = pipeline.Fit(dataView);
            var transformedData = model.Transform(dataView);

            // ベクトル化された特徴量を取得
            var featureColumn = transformedData.GetColumn<float[]>("Features").ToArray();
            var ids = Enumerable.Range(0, featureColumn.Length).Select(i => (long)i).ToArray(); // IDはユニークな値である必要があります


            // FaissNetインデックスの作成
            int dimension = featureColumn[0].Length; // ベクトルの次元数
            using (var index = FaissNet.Index.CreateDefault(dimension, MetricType.METRIC_L2))
            {
                index.AddWithIds(featureColumn, ids); // ベクトルとIDを追加


                // 検索クエリ（探したい文字列をベクトル化）
                var searchQuery = new TextData { Text = text };
                var searchDataView = mlContext.Data.LoadFromEnumerable(new[] { searchQuery });
                var searchTransformed = model.Transform(searchDataView);
                var searchVector = searchTransformed.GetColumn<float[]>("Features").First();

                // 類似性検索の実行
                var (distances, neighbors) = index.Search(new[] { searchVector }, 1);

                // 最も類似しているデータを表示
                var nearestNeighborIndex = neighbors[0][0]; // 最も近いインデックス
                System.Diagnostics.Debug.Print("Query Text: " + searchQuery.Text);
                System.Diagnostics.Debug.Print("Nearest Neighbors: " + nearestNeighborIndex.ToString());

                string hitPath = filePathList[nearestNeighborIndex];
                using (System.IO.TextReader reader = new System.IO.StreamReader(hitPath))
                {
                    text = reader.ReadToEnd();
                }
                return text;
            }
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

            //foreach (JsonElement jsonElement in messagesAsJson.RootElement.EnumerateArray())
            //{
            //    yield return ModelReaderWriter.Read<ChatMessage>(BinaryData.FromObjectAsJson(jsonElement), ModelReaderWriterOptions.Json);
            //}
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

            //using MemoryStream stream = new();
            //using Utf8JsonWriter writer = new(stream);

            //writer.WriteStartArray();

            //foreach (IJsonModel<ChatMessage> message in messages)
            //{
            //    message.Write(writer, ModelReaderWriterOptions.Json);
            //}

            //writer.WriteEndArray();
            //writer.Flush();

            //return BinaryData.FromBytes(stream.ToArray());
        }
        public void ClearChat()
        {
            chatMessages.Clear();
        }
    }
}
