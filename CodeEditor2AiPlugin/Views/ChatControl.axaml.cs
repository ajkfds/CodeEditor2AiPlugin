using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static CodeEditor2AiPlugin.Views.ChatControl;
using static System.Net.Mime.MediaTypeNames;

namespace CodeEditor2AiPlugin.Views;

public partial class ChatControl : UserControl,ILLMChat
{
    public ObservableCollection<ChatItem> Items { get; set; } = new ObservableCollection<ChatItem>();
    public ChatControl()
    {
        DataContext = this;
        InitializeComponent();

        Items.Add(new TextItem("DeepSeek-R1\n"));
        Items.Add( inputItem );
        
        inputItem.TextBox.TextChanged += TextBox_TextChanged;
        var keyBinding = new KeyBinding
        {
            Gesture = new KeyGesture(Key.Enter, KeyModifiers.Control), // Ctrl+Enter
            Command = ReactiveCommand.Create(() => {
                enterCommand();
            }),
        };
        inputItem.TextBox.KeyBindings.Add(keyBinding);
    }


    private void TextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        if (textBox.Text == null) return;

        var lineCount = textBox.Text.Split(Environment.NewLine).Length;
        textBox.Height = lineCount * this.FontSize * 1.5 + 10;
    }

    OpenRouterChat chat = new OpenRouterChat();
    InputItem inputItem = new InputItem();
    TextItem? lastResultItem = null;

    private Avalonia.Media.Color commandColor = new Avalonia.Media.Color(255, 255, 200, 200);
    private Avalonia.Media.Color completeColor = new Avalonia.Media.Color(255, 200, 255, 255);

    bool inputAcceptable = true;

    private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();


    private void enterCommand()
    {
        _= Complete(cancellationTokenSource.Token);
    }

    private async Task Complete(CancellationToken cancellation)
    {
        if (!inputAcceptable) return;
        inputAcceptable = false;

        string? command = inputItem.TextBox.Text;
        if (command == null) return;

        TextItem commandItem = new TextItem(command);
        inputItem.TextBox.Text = "";
        commandItem.TextColor = commandColor;
        Items.Insert(Items.Count - 1, commandItem);

        TextItem resultItem = new TextItem("");
        Items.Insert(Items.Count - 1, resultItem);
        lastResultItem = resultItem;

        // progress
        var stopwatch = Stopwatch.StartNew();

        using var cts = new CancellationTokenSource();
        var displayTimerTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    await resultItem.SetText("waiting ... " + (stopwatch.ElapsedMilliseconds / 1000f).ToString("F1") + "s");
                    await Task.Delay(100, cts.Token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }, cts.Token);

        bool timerActivate = true;

        await foreach (string ret in chat.GetAsyncCollectionChatResult(command, cancellation))
        {
            if (timerActivate & ret != "")
            {
                cts.Cancel();
                await resultItem.SetText("");
                timerActivate = false;
            }
            await resultItem.AppendText(ret);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ListBox0.ScrollIntoView(inputItem);
            });
        }
        stopwatch.Stop();
        await displayTimerTask;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            resultItem.TextColor = completeColor;
        });

        inputAcceptable = true;
    }

    public async IAsyncEnumerable<string> GetAsyncCollectionChatResult(string command, [EnumeratorCancellation] CancellationToken cancellation)
    {
        inputItem.TextBox.Text = command;
        await Complete(cancellation);
        if(lastResultItem == null) yield break;
        yield return lastResultItem.Text;
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
    // chat display items
    public class ChatItem : ListBoxItem
    {
    }

    public class TextItem : ChatItem
    {
        private TextBlock textBlock;
        public TextItem(string text,Avalonia.Media.Color textColor): this(text)
        {
            TextColor = textColor;
        }
        public TextItem(string text)
        {
            textBlock = new TextBlock()
            {
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Thickness(10, 5, 10, 5)
            };
            textBlock.Text = text;
            Content = textBlock;
            textBlock.PointerEntered += (sender, e) =>
            {
                Background = new Avalonia.Media.SolidColorBrush(new Avalonia.Media.Color(50, 0, 0, 100));
            };
            textBlock.PointerExited += (sender, e) =>
            {
                Background = Avalonia.Media.Brushes.Transparent;
            };

            ContextMenu contextMenu = new ContextMenu();
            {
                MenuItem menuItem = new MenuItem()
                {
                    Header = "Copy"
                };
                menuItem.Click += (sender, e) => {
                    var top = TopLevel.GetTopLevel(this);
                    top?.Clipboard?.SetTextAsync(textBlock.Text);
                };
                contextMenu.Items.Add(menuItem);
            }

            textBlock.ContextMenu = contextMenu;
        }

        public Avalonia.Media.Color TextColor
        {
            set
            {
                textBlock.Foreground = new Avalonia.Media.SolidColorBrush(value);
            }
        }
        public async Task SetText(string text)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                textBlock.Text = text;
            });
        }

        public string Text
        {
            get
            {
                if(textBlock.Text == null) return "";   
                return textBlock.Text;
            }
        }
        public async Task AppendText(string text)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                textBlock.Text += text;
            });
        }
    }
    public class InputItem : ChatItem
    {
        public InputItem()
        {
            Content = TextBox;
            TextBox.Margin = new Thickness(10, 5, 10, 5);
            TextBox.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        }

        public TextBox TextBox = new TextBox()
        {
            Margin = new Thickness(10, 5, 10, 5),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            AcceptsReturn = true
        };
    }
}

