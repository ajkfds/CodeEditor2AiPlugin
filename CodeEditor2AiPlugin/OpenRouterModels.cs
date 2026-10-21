using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace pluginAi
{
    public static class OpenRouterModels
    {
        // openrouter models
        // https://openrouter.ai/models

        /// <summary>
        /// Enumerates all static Model fields declared in this class (in declaration order).
        /// Keeps GetAvailableModels() in sync with the model list without manual duplication.
        /// </summary>
        public static List<Model> GetAllModels()
        {
            return typeof(OpenRouterModels)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(Model))
                .OrderBy(f => f.MetadataToken) // declaration order
                .Select(f => (Model)f.GetValue(null)!)
                .Where(m => m != null)
                .ToList();
        }

        public static Model deepseek_4_1_flash_1017 = new Model("deepseek/deepseek-v4.1-flash-1017", "DeepSeek: 4.1 Flash 1017");

        public static Model deepseek_4_1_flash = new Model("deepseek/deepseek-v4.1-flash", "DeepSeek: 4.1 Flash");
        public static Model z_ai_glm_5_3_flash = new Model("z-ai/glm-5.3-flash", "Z.ai: GLM 5.3 Flash");

        public static Model openai_gpt_oss_120b = new Model("openai/gpt-oss-120b", "OpenAI: gpt-oss-120b");
        public static Model deepseek_deepseek_v3_2 = new Model("deepseek/deepseek-v3.2", "DeepSeek: DeepSeek V3.2");


        public static Model z_ai_glm_5_2 = new Model("z-ai/glm-5.2", "z-ai glm-5.2");

        public static Model minimax_minimax_m2_5 = new Model("minimax/minimax-m2.5", "MiniMax: MiniMax M2.5");
        public static Model minimax_minimax_m2_7 = new Model("minimax/minimax-m2.7", "MiniMax: MiniMax M2.7");
        public static Model minimax_minimax_m3 = new Model("minimax/minimax-m3", "MiniMax: MiniMax M3");

        public static Model moonshotai_kimi_k2_5 = new Model("moonshotai/kimi-k2.5", "MoonshotAI: Kimi K2.5");
        public static Model anthropic_claude_3p7_sonnet = new Model("anthropic/claude-3.7-sonnet", "Anthropic: Claude 3.7 Sonnet");
        public static Model google_gemini_3_pro_preview = new Model("google/gemini-3-pro-preview", "Google: Gemini 3 Pro Preview");

        public static Model google_gemini_2p5_flash_lite = new Model("google/gemini-2.5-flash-lite", "Google: Gemini 2.5 Flash Lite");
        public static Model openai_gpt_oss_20b = new Model("openai/gpt-oss-20b", "OpenAI: gpt-oss-20b");
        public static Model openai_gpt_4p1_nano = new Model("openai/gpt-4.1-nano", "OpenAI: GPT-4.1 Nano");
        public static Model xiaomi_mimo_v2_flash_free = new Model("xiaomi/mimo-v2-flash:free", "Xiaomi: MiMo-V2-Flash (free)");
        public static Model xiaomi_mimo_v2_5_pro = new Model("xiaomi/mimo-v2.5-pro", "xiaomi/mimo-v2.5-pro");

        public static Model google_gemini_3_flash_preview = new Model("google/gemini-3-flash-preview", "Google: Gemini 3 Flash Preview");
        public static Model openai_gpt_5_1_codex_mini = new Model("openai/gpt-5.1-codex-mini", "OpenAI: GPT-5.1-Codex-Mini");



        // free models (unstable)
        public static Model openai_gpt_oss_20b_free = new Model("openai/gpt-oss-20b:free", "OpenAI: gpt-oss-20b (free)");
        public class Model
        {
            public Model(string name, string caption)
            {
                Name = name;
                Caption = caption;
            }
            public string Name { init; get; }
            public string Caption { init; get; }
        }
    }
}
