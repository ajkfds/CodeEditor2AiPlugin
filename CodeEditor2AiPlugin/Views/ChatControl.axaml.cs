using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using static CodeEditor2AiPlugin.Views.ChatControl;
using static System.Net.Mime.MediaTypeNames;

namespace CodeEditor2AiPlugin.Views;

public partial class ChatControl : UserControl
{
    public ObservableCollection<ChatItem> Items { get; set; } = new ObservableCollection<ChatItem>();
    public ChatControl()
    {
        DataContext = this;
        InitializeComponent();

        Items.Add(new TextItem("DeepSeek-R1\n"));
        Items.Add( inputItem );
        
        inputItem.TextBox.KeyDown += TextBox_KeyDown;
    }
    OpenRouterChat chat = new OpenRouterChat();
    InputItem inputItem = new InputItem();

    private Avalonia.Media.Color commandColor = new Avalonia.Media.Color(255, 255, 200, 200);
    private Avalonia.Media.Color completeColor = new Avalonia.Media.Color(255, 200, 255, 255);

    bool inputAcceptable = true;
    private async void TextBox_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key != Avalonia.Input.Key.Enter) return;
        if (!inputAcceptable) return;
        inputAcceptable = false;

        string? command = inputItem.TextBox.Text;
        if(command==null) return;

        TextItem commandItem = new TextItem(command);
        inputItem.TextBox.Text = "";
        commandItem.TextColor = commandColor;
        Items.Insert(Items.Count - 1, commandItem);

        TextItem resultItem = new TextItem("");
        Items.Insert(Items.Count - 1, resultItem);
        
        // progress
        var stopwatch = Stopwatch.StartNew();

        using var cts = new CancellationTokenSource();
        var displayTimerTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    await resultItem.SetText("waiting ... " + (stopwatch.ElapsedMilliseconds/1000f).ToString("F1") + "s");
                    await Task.Delay(100, cts.Token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }, cts.Token);

        bool timerActivate = true;

        await foreach (string ret in chat.GetAsyncCollectionChatResult(command))
        {
            if (timerActivate & ret !="")
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

        public TextBox TextBox = new TextBox();
    }
}

