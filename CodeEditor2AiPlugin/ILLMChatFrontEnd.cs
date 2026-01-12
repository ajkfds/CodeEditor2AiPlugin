using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace pluginAi
{
    public interface ILLMChatFrontEnd
    {
        IAsyncEnumerable<string> GetAsyncCollectionChatResult(string command, IList<AITool>? tools, CancellationToken cancellation);
        Task<string> GetAsyncChatResult(string command, IList<AITool>? tools, CancellationToken cancellationToken);

        Task SaveMessagesAsync(string filePath);
        Task LoadMessagesAsync(string filePath);

        Task ResetAsync();
    }
}
