using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static pluginAi.Views.ChatControl;
using static System.Net.Mime.MediaTypeNames;

namespace pluginAi.Views;

public partial class ChatControl : UserControl,ILLMChat
{
    public ObservableCollection<ChatItem> Items { get; set; } = new ObservableCollection<ChatItem>();
    public ChatControl()
    {
        DataContext = this;
        InitializeComponent();

        if (Design.IsDesignMode)
        {
            return;
        }

        //this.scrollViewer = scrollViewer;
        //scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;

        Items.Add(new TextItem("DeepSeek-R1\n"));
        Items.Add( inputItem );


        inputItem.TextBox.TextChanged += TextBox_TextChanged;
        var keyBinding = new KeyBinding
        {
            Gesture = new KeyGesture(Key.Enter, KeyModifiers.None ),
            Command = ReactiveCommand.Create(() =>
            {
                inputItem.SendButton.Focus();
//                enterCommand();
            }),
        };
        inputItem.TextBox.KeyBindings.Add(keyBinding);
        inputItem.SendButton.Click += SendButton_Click;
        inputItem.SaveButton.Click += SaveButton_Click;
        inputItem.ClearButton.Click += ClearButton_Click;
        inputItem.LoadButton.Click += LoadButton_Click;
        inputItem.TestButton.Click += TestButton_Click;

        inputItem.TextBox.Focus();
        ListBox0.Loaded += ListBox0_Loaded;
    }

    private ScrollViewer scrollViewer;
    private void ListBox0_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var scrollViewer = ListBox0.FindDescendantOfType<ScrollViewer>();
        if (scrollViewer == null) throw new Exception();
        scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
        this.scrollViewer= scrollViewer;
    }

    private void ScrollViewer_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        // スクロール可能な高さの閾値（例: 100px手前でトリガー）
        const double triggerThreshold = 100;
        var triggerPosition = scrollViewer.Extent.Height - scrollViewer.Viewport.Height - triggerThreshold;

        if (scrollViewer.Offset.Y >= triggerPosition)
        {
            autoScroll = true;
        }
        else
        {
            autoScroll = false;
        }
    }

    private bool autoScroll { get; set; } = true;

    private void TestButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string? command = inputItem.TextBox.Text;
        if (command == null) return;

//        chat.GenerateFileIndex();
        string rag = chat.GetRagText(command, "references");

        StringBuilder sb = new StringBuilder();

        sb.Append("以下の参考文献を元にユーザの質問に答えてください。\r\n");
        sb.Append("# 参考文献\r\n");
        sb.Append(rag);
        sb.Append("\r\n");
        sb.Append("# ユーザの質問\r\n");
        sb.Append(command);

        _ = Complete(sb.ToString(), cancellationTokenSource.Token);

    }

    public void LoadLogFile()
    {
        if (LogFilePath == null) return;
        chat.LoadMessages(LogFilePath);
    }

    public void SaveLogFile()
    {
        if (LogFilePath == null) return;
        chat.SaveMessages(LogFilePath);
    }

    public bool AutoSave { set; get; } = false;
    public string? LogFilePath { set; get; } = null;

    private void LoadButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        LoadLogFile();
    }

    private void ClearButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        chat.ClearChat();
    }

    private void SaveButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SaveLogFile();
    }

    private void SendButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string? command = inputItem.TextBox.Text;
        if(command == null) return;

        _ = Complete(command,cancellationTokenSource.Token);
        inputItem.TextBox.Focus();
    }

    private void TextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        if (textBox.Text == null) return;

        // expand textbox height
        UpdateTextBoxHeight(textBox);
    }
    private void UpdateTextBoxHeight(TextBox textBox)
    {
        if (textBox == null || !textBox.IsMeasureValid)
            return;

        var padding = textBox.Padding;
        var borderThickness = textBox.BorderThickness;
        double availableWidth = textBox.Bounds.Width - padding.Left - padding.Right - borderThickness.Left - borderThickness.Right;

        if (availableWidth <= 0)
            return;

        var typeface = new Typeface(textBox.FontFamily, textBox.FontStyle, textBox.FontWeight);
        var formattedText = new Avalonia.Media.FormattedText(textBox.Text, System.Globalization.CultureInfo.CurrentCulture,FlowDirection.LeftToRight, typeface, textBox.FontSize, textBox.Foreground);

        double textHeight = formattedText.Height;//.Bounds.Height;
        double requiredHeight = textHeight + padding.Top + padding.Bottom + borderThickness.Top + borderThickness.Bottom;
        requiredHeight = Math.Max(requiredHeight, textBox.FontSize + padding.Top + padding.Bottom + borderThickness.Top + borderThickness.Bottom);

        if (Math.Abs(textBox.Height - requiredHeight) > 0.1)
            textBox.Height = requiredHeight;
    }

    OpenRouterChat chat = new OpenRouterChat();
    InputItem inputItem = new InputItem();
    TextItem? lastResultItem = null;

    private Avalonia.Media.Color commandColor = new Avalonia.Media.Color(255, 255, 200, 200);
    private Avalonia.Media.Color completeColor = new Avalonia.Media.Color(255, 200, 255, 255);

    bool inputAcceptable = true;

    private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();



    private async Task Complete(string command,CancellationToken cancellation)
    {
        string? input = inputItem.TextBox.Text;
        if(input == null) return;

        // reentrant lock
        if (!inputAcceptable) return;
        inputAcceptable = false;

        TextItem commandItem = new TextItem(input);
        inputItem.TextBox.Text = "";
        commandItem.TextColor = commandColor;
        Items.Insert(Items.Count - 1, commandItem);

        TextItem resultItem = new TextItem("");
        Items.Insert(Items.Count - 1, resultItem);
        lastResultItem = resultItem;

        // show progress timer
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

        bool timerActivate = true; // timer activate flag, timer will be stopped when first result is returned

        // execute chat command
        await foreach (string ret in chat.GetAsyncCollectionChatResult(command, cancellation))
        {
            if (timerActivate & ret != "")
            {
                cts.Cancel();
                await resultItem.SetText("");
                timerActivate = false;
            }
            await resultItem.AppendText(ret);
            if(autoScroll)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ListBox0.ScrollIntoView(inputItem);
                });
            }
        }
        stopwatch.Stop();
        await displayTimerTask;

        // change color to complete color
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            resultItem.TextColor = completeColor;
            SaveLogFile();
        });

        inputAcceptable = true;
    }

    public async IAsyncEnumerable<string> GetAsyncCollectionChatResult(string command, [EnumeratorCancellation] CancellationToken cancellation)
    {
        inputItem.TextBox.Text = command;
        await Complete(command,cancellation);
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
        private TextBox textBox = new TextBox()
        {
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Margin = new Thickness(10, 5, 10, 5),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            AcceptsReturn = true,
            IsReadOnly = true
        };

        public TextItem(string text,Avalonia.Media.Color textColor): this(text)
        {
            TextColor = textColor;
        }
        public TextItem(string text)
        {
            textBox.Text = text;
            Content = textBox;

            textBox.PointerEntered += (sender, e) =>
            {
                Background = new Avalonia.Media.SolidColorBrush(new Avalonia.Media.Color(50, 0, 0, 100));
            };
            textBox.PointerExited += (sender, e) =>
            {
                Background = Avalonia.Media.Brushes.Transparent;
            };

            ContextMenu contextMenu = new ContextMenu();
            {
                MenuItem menuItem = new MenuItem()
                {
                    Header = "Copy"
                };
                menuItem.Click += (sender, e) =>
                {
                    var top = TopLevel.GetTopLevel(this);
                    top?.Clipboard?.SetTextAsync(textBox.Text);
                };
                contextMenu.Items.Add(menuItem);
            }

            textBox.ContextMenu = contextMenu;
            textBox.BorderBrush = new Avalonia.Media.SolidColorBrush(new Avalonia.Media.Color(255, 10, 10, 10));
        }


        public Avalonia.Media.Color TextColor
        {
            set
            {
                textBox.Foreground = new Avalonia.Media.SolidColorBrush(value);
            }
        }
        public async Task SetText(string text)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                textBox.Text = text;
            });
        }

        public string Text
        {
            get
            {
                if(textBox.Text == null) return "";   
                return textBox.Text;
            }
        }
        public async Task AppendText(string text)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                textBox.Text += text;
            });
        }
    }

    public class InputItem : ChatItem
    {
        public InputItem()
        {
            Content = StackPanel;
            TextBox.Margin = new Thickness(10, 5, 10, 5);
            TextBox.TextWrapping = Avalonia.Media.TextWrapping.Wrap;

            StackPanel.Children.Add(TextBox);
            StackPanel.Children.Add(ButtonBar);
            {
                ButtonBar.Children.Add(TestButton);
                ButtonBar.Children.Add(ClearButton);
                ButtonBar.Children.Add(LoadButton);
                ButtonBar.Children.Add(SaveButton);
                ButtonBar.Children.Add(SendButton);
            }


            SendButton.PropertyChanged += (sender, args) =>
            {
                if (args.Property == Button.IsFocusedProperty)
                {
                    var isFocused = (bool)args.NewValue!;
                    if (isFocused)
                    {
                        SendButton.Background = new Avalonia.Media.SolidColorBrush(new Avalonia.Media.Color(255, 0, 120, 212));
                    }
                    else
                    {
                        SendButton.Background = new Avalonia.Media.SolidColorBrush(new Avalonia.Media.Color(255, 20, 20, 20));
                    }
                }
            };

            SaveButton.Click += async (o, e) =>
            {
                SendButton.Background = new Avalonia.Media.SolidColorBrush(new Avalonia.Media.Color(255, 0, 120, 212));
                await Task.Delay(100);
                SendButton.Background = new Avalonia.Media.SolidColorBrush(new Avalonia.Media.Color(255, 20, 20, 20));
            };
        }

        public static readonly StyledProperty<IBrush?> SelectionBrushProperty =
            AvaloniaProperty.Register<TextBox, IBrush?>(nameof(SelectionBrush));
        public IBrush? SelectionBrush
        {
            get => GetValue(SelectionBrushProperty);
            set => SetValue(SelectionBrushProperty, value);
        }

        public StackPanel StackPanel = new StackPanel()
        {
            Orientation = Avalonia.Layout.Orientation.Vertical,
            Margin = new Thickness(10, 5, 10, 5),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
        };

        public TextBox TextBox = new TextBox()
        {
            Margin = new Thickness(10, 5, 10, 5),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            AcceptsReturn = true
        };

        public StackPanel ButtonBar = new StackPanel()
        {
            Background = new Avalonia.Media.SolidColorBrush(new Avalonia.Media.Color(255, 20, 20, 20)),
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Margin = new Thickness(10, 5, 10, 5),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };

        public Button SendButton = new Button()
        {
            Content = "Send",
            Margin = new Thickness(0, 0, 0, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
        };

        public Button SaveButton = new Button()
        {
            Content = "Save",
            Margin = new Thickness(0, 0, 0, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
        };

        public Button LoadButton = new Button()
        {
            Content = "Load",
            Margin = new Thickness(0, 0, 0, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
        };

        public Button ClearButton = new Button()
        {
            Content = "Clear",
            Margin = new Thickness(0, 0, 0, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
        };

        public Button TestButton = new Button()
        {
            Content = "Test",
            Margin = new Thickness(0, 0, 0, 0),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
        };

    }
}

