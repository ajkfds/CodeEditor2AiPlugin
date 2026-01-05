using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pluginAi.LLMWrapper
{
    public interface LightLLM
    {
        public Task<string?> AskAsync(string prompt,System.Threading.CancellationToken cancellationToken);

        public Task ResetAsync();
    }

}
