using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenAI.Chat;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.OpenAI; // 拡張メソッドのために必要


namespace pluginAi
{
    internal class ResponceAPI
    {
        public async Task test()
        {
            // 1. OpenAI公式の ChatClient を作成
            ChatClient openAIChatClient = new("gpt-4o", "YOUR_OPENAI_API_KEY");

            // 2. ChatClient から直接 AIエージェント（応答クライアント）を生成！
            //    ここでペルソナや指示（Instructions）を与えられます
            var responseAgent = openAIChatClient.AsAIAgent(
                name: "AssistantAgent",
                instructions: "あなたは優秀なC#のエンジニアです。簡潔に回答してください。"
            );

            // 3. 応答の実行 (1回限りの呼び出し)
            var response = await responseAgent.RunAsync("非同期処理の注意点を1つ教えて");
            Console.WriteLine(response.Text);

            // ※ ストリーミング応答の場合
            // await foreach (var chunk in responseAgent.RunStreamingAsync("プロンプト")) { ... }

        }

    }
}
