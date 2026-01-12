using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginAi
{
    public static class OpenRouterModels
    {
        // free openrouter models
        // https://openrouter.ai/models?max_price=0


        public static Model google_gemini_2p5_flash_lite    = new Model("google/gemini-2.5-flash-lite", "Google: Gemini 2.5 Flash Lite", 1_048_576,0.10,0.40);
        public static Model openai_gpt_oss_20b_free         = new Model("openai/gpt-oss-20b:free", "OpenAI: gpt-oss-20b (free)", 131_072, 0, 0);
        public static Model openai_gpt_oss_20b               = new Model("openai/gpt-oss-20b", "OpenAI: gpt-oss-20b", 131_072, 0.016, 0.06);
        public static Model openai_gpt_oss_120b             = new Model("openai/gpt-oss-120b", "OpenAI: gpt-oss-120b", 131_072, 0.02, 0.10);
        public static Model openai_gpt_4p1_nano             = new Model("openai/gpt-4.1-nano", "OpenAI: GPT-4.1 Nano", 1_047_576, 0.10, 0.40);// $10/K web search)
        public static Model xiaomi_mimo_v2_flash_free = new Model("xiaomi/mimo-v2-flash:free", "Xiaomi: MiMo-V2-Flash (free)", 262_144, 0, 0);
        public static Model anthropic_claude_3p7_sonnet = new Model("anthropic/claude-3.7-sonnet", "Anthropic: Claude 3.7 Sonnet", 200_000, 3, 15);
        public class Model
        {
            public Model(string name,string caption)
            {
                Name = name;
                Caption = caption;
            }
            public Model(string name, string caption,int contextSize, double inputDollarPricePerMTokens, double outputDollarPricePerMTokens)
            {
                Name = name;
                Caption = caption;
                InputDollarPricePerMTokens = inputDollarPricePerMTokens;
                OutputDollarPricePerMTokens = outputDollarPricePerMTokens;
            }
            public string Name { init; get; }
            public string Caption { init; get; }
            public int ContextSize { init; get; }
            public double? InputDollarPricePerMTokens { init; get; }
            public double? OutputDollarPricePerMTokens { init; get; }
        }
    }
}
