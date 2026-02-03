using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace pluginAi
{
    public static class OpenRouterModels
    {
        // free openrouter models
        // https://openrouter.ai/models

        public static Model openai_gpt_oss_120b = new Model("openai/gpt-oss-120b", "OpenAI: gpt-oss-120b", 131_072, 0.02, 0.10);
        public static Model deepseek_deepseek_v3_2 = new Model("deepseek/deepseek-v3.2", "DeepSeek: DeepSeek V3.2", 163_840, 0.25, 0.38);

        public static Model anthropic_claude_3p7_sonnet = new Model("anthropic/claude-3.7-sonnet", "Anthropic: Claude 3.7 Sonnet", 200_000, 3, 15);
        public static Model google_gemini_3_pro_preview = new Model("google/gemini-3-pro-preview", "Google: Gemini 3 Pro Preview", 1_048_576, 2, 12);

        public static Model google_gemini_2p5_flash_lite    = new Model("google/gemini-2.5-flash-lite", "Google: Gemini 2.5 Flash Lite", 1_048_576,0.10,0.40);
        public static Model openai_gpt_oss_20b               = new Model("openai/gpt-oss-20b", "OpenAI: gpt-oss-20b", 131_072, 0.016, 0.06);
        public static Model openai_gpt_4p1_nano             = new Model("openai/gpt-4.1-nano", "OpenAI: GPT-4.1 Nano", 1_047_576, 0.10, 0.40);// $10/K web search)
        public static Model xiaomi_mimo_v2_flash_free = new Model("xiaomi/mimo-v2-flash:free", "Xiaomi: MiMo-V2-Flash (free)", 262_144, 0, 0);
        public static Model google_gemini_3_flash_preview = new Model("google/gemini-3-flash-preview","Google: Gemini 3 Flash Preview", 1_048_576,0.50,3);
        public static Model openai_gpt_5_1_codex_mini = new Model("openai/gpt-5.1-codex-mini", "OpenAI: GPT-5.1-Codex-Mini", 400_000, 0.25, 2);

        // free models (unstable)
        public static Model openai_gpt_oss_20b_free = new Model("openai/gpt-oss-20b:free", "OpenAI: gpt-oss-20b (free)", 131_072, 0, 0);
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
