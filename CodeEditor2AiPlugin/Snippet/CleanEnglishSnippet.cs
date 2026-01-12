using Avalonia.Input;
using Avalonia.Threading;
using CodeEditor2.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace pluginAi.Snippet
{
    public class CleanEnglishSnippet : CodeEditor2.Snippets.InteractiveSnippet
    {
        public CleanEnglishSnippet() : base("cleanEnglish")
        {
            IconImage = AjkAvaloniaLibs.Libs.Icons.GetSvgBitmap(
                    "CodeEditor2/Assets/Icons/wrench.svg",
                    Avalonia.Media.Colors.OrangeRed
                    );
        }

        private static LLMChat? LLM;

        public static Func<LLMChat>? GetLLM = null;


        private CodeEditor2.CodeEditor.CodeDocument? document;

        public override void Apply()
        {
            if(GetLLM == null)
            {
                CodeEditor2.Controller.CodeEditor.AbortInteractiveSnippet();
                return;
//                LLM = new LLMChat(new OpenRouterChat(OpenRouterModels.openai_gpt_oss_20b,false));
            }
            else
            {
                LLM = GetLLM();
            }


            CodeEditor2.Data.TextFile? file = CodeEditor2.Controller.CodeEditor.GetTextFile();
            if (file == null) return;
            document = file.CodeDocument;
            if (document == null) return;

            // set highlights for {n} texts
            CodeEditor2.Controller.CodeEditor.ClearHighlight();
            CodeEditor2.Controller.CodeEditor.GetSelection(out int selectionStart,out int selectionEnd);
            CodeEditor2.Controller.CodeEditor.AppendHighlight(selectionStart, selectionEnd);

            base.Apply();

            // run async task
            System.Threading.Tasks.Task.Run(RunAsync);
        }

        // backrtound thread ------------------------------------------------------

        private static System.Threading.Tasks.Task? _currentTask;
        private static CancellationTokenSource? _cts;
        private async System.Threading.Tasks.Task RunAsync()
        {
            if (_cts != null)
            {
                _cts.Cancel();
            }

            _cts = new CancellationTokenSource();
            CancellationToken token = _cts.Token;

            _currentTask = System.Threading.Tasks.Task.Run(async () =>
            {
                await runBackGround(token);
            }, token);

            try
            {
                await _currentTask;
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
            }
            finally
            {
                _currentTask = null;
                await CodeEditor2.Controller.CodeEditor.AbortInteractiveSnippetAsync();
            }
            return;
        }

        //        private static LLMCopilotOnBrowser? llm = null;
        private TaskCompletionSource<string> _eventTcs; // return from UI thread
        private async System.Threading.Tasks.Task runBackGround(CancellationToken token)
        {
            try
            {
                if (document == null) return;
                if (LLM == null) return;

                int start = 0;
                int end = 0;
                await Dispatcher.UIThread.InvokeAsync(() => {
                    CodeEditor2.Controller.CodeEditor.GetHighlightPosition(0, out start, out end);
                });
                string text = document.CreateString(start, end - start);

                StringBuilder sb = new StringBuilder();
                sb.Append("以下の文章を正しい英語にして、変換後の英語の文章のみを回答して。\n");
                sb.Append("\n");
                sb.Append("---\n");
                sb.Append(text);
                string prompt = sb.ToString();

                
                string? responce = await LLM.AskAsync(prompt,token);
                if (responce != null)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        document.Replace(start, end-start, 0, responce);
                    });
                }
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    CodeEditor2.Controller.CodeEditor.AbortInteractiveSnippet();
                });
            }
            catch (Exception ex)
            {
                CodeEditor2.Controller.AppendLog("##Exception " + ex.Message, Avalonia.Media.Colors.Red);
            }
            finally
            {
                await CodeEditor2.Controller.CodeEditor.AbortInteractiveSnippetAsync();
            }
        }

        // UI thread handler ------------------------------------------------------

        public override void Aborted()
        {
            if (_cts != null) _cts.Cancel();
            CodeEditor2.Controller.CodeEditor.ClearHighlight();
            document = null;
            base.Aborted();
        }


        public override void KeyDown(object? sender, KeyEventArgs e, PopupMenuView popupMenuView)
        {
            System.Diagnostics.Debug.Print("## AlwaysFFSnippet.KeyDown");

            // overrider return & escape
            if (!CodeEditor2.Controller.CodeEditor.IsPopupMenuOpened)
            {
                if (e.Key == Key.Escape || e.Key == Key.Up)
                {
                    CodeEditor2.Controller.CodeEditor.AbortInteractiveSnippet();
                    e.Handled = true;
                }
            }
        }
        public override void BeforeKeyDown(object? sender, TextInputEventArgs e, CodeEditor2.Views.PopupMenuView popupMenuView)
        {
            System.Diagnostics.Debug.Print("## AlwaysFFSnippet.BeforeKeyDown");
        }
        public override void AfterKeyDown(object? sender, TextInputEventArgs e, CodeEditor2.Views.PopupMenuView popupMenuView)
        {
            System.Diagnostics.Debug.Print("## AlwaysFFSnippet.AfterKeyDown");
        }
        public override void AfterAutoCompleteHandled(CodeEditor2.Views.PopupMenuView popupMenuView)
        {
            if (document == null) return;
            System.Diagnostics.Debug.Print("## AlwaysFFSnippet.AfterAutoCompleteHandled");
            if (_eventTcs != null) _eventTcs.TrySetResult("moveNext");
        }

        // return @ carlet line changed
        public override void Caret_PositionChanged(object? sender, EventArgs e)
        {
            if (document == null) return;

            int? carletPosition = CodeEditor2.Controller.CodeEditor.GetCaretPosition();
            if (carletPosition == null) return;
            int line = document.GetLineAt((int)carletPosition);

        }

    }
}
