using pluginAi.LLMWrapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace pluginAi.LLMWapper
{
    public class LightGptOssFree : LightLLM
    {
        public LightGptOssFree()
        {
            chat = new OpenRouterChat(OpenRouterModels.openai_gpt_oss_20b);
        }
        private OpenRouterChat chat { init; get; }

        public async Task<string?> AskAsync(string prompt, System.Threading.CancellationToken cancellationToken)
        {
            string ret = await chat.GetAsyncChatResult(prompt,null,cancellationToken);
            return ret;
        }

        public async Task ResetAsync()
        {
            await chat.ResetAsync();
        }
    }
}
