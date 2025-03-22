using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CodeEditor2AiPlugin
{
    public interface ILLMChat
    {
        IAsyncEnumerable<string> GetAsyncCollectionChatResult(string command, CancellationToken cancellation);
        Task<string> GetAsyncChatResult(string command, CancellationToken cancellationToken);
    }
}
