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
using Microsoft.Extensions.AI;
using Microsoft.ML;
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

public partial class ChatControl : UserControl,ILLMChatFrontEnd
{
    public ObservableCollection<ChatItem> Items { get; set; } = new ObservableCollection<ChatItem>();

    public OpenRouterModels.Model Model { get; protected set; }

    public ChatControl()
    {
        DataContext = this;
        InitializeComponent();

        if (Design.IsDesignMode)
        {
            return;
        }

        Model = OpenRouterModels.openai_gpt_oss_20b_free;


        chat = new OpenRouterChat(Model,false);

        Items.Add( new TextItem(Model.Caption+"\n") );
        Items.Add( inputItem );


        inputItem.TextBox.TextChanged += TextBox_TextChanged;
        var keyBinding = new KeyBinding
        {
            Gesture = new KeyGesture(Key.Enter, KeyModifiers.None ),
            Command = ReactiveCommand.Create(() =>
            {
                inputItem.SendButton.Focus();
            }),
        };
        inputItem.TextBox.KeyBindings.Add(keyBinding);
        inputItem.SendButton.Click += SendButton_Click;
        inputItem.SaveButton.Click += SaveButton_Click;
        inputItem.ClearButton.Click += ClearButton_Click;
        inputItem.LoadButton.Click += LoadButton_Click;
        inputItem.TestButton.Click += TestButton_Click;
        inputItem.AbortButton.Click += AbortButton_Click;
        inputItem.TextBox.Focus();
        ListBox0.Loaded += ListBox0_Loaded;


    }


    public Task SetModelAsync(OpenRouterModels.Model model,bool enableFunctionCalling)
    {
        Model = model;
        chat = new OpenRouterChat(Model, enableFunctionCalling);
        return Task.CompletedTask;
    }

    private void AbortButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        cancellationTokenSource.Cancel();
    }

    private ScrollViewer scrollViewer;
    private bool _isInternalScrolling = false; // システムによるスクロール中かどうかのフラグ

    private void ListBox0_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var scrollViewer = ListBox0.FindDescendantOfType<ScrollViewer>();
        if (scrollViewer == null) throw new Exception();

        scrollViewer = ListBox0.FindDescendantOfType<ScrollViewer>();
        if (scrollViewer == null) return;

        //scrollViewer.GetObservable(ScrollViewer.ExtentProperty).Subscribe(_ =>
        //{
        //    if (autoScroll)
        //    {
        //        // Extentが変わった（テキストが増えた）瞬間にOffsetを更新
        //        scrollViewer.Offset = new Vector(scrollViewer.Offset.X, double.MaxValue);
        //    }
        //});

        // 1. 高さ（中身）が変わった時の処理
        scrollViewer.GetObservable(ScrollViewer.ExtentProperty).Subscribe(_ =>
        {
            if (autoScroll)
            {
                _isInternalScrolling = true; // 「今からシステムが動かします」という合図
                scrollViewer.Offset = new Vector(scrollViewer.Offset.X, double.MaxValue);

                // 描画サイクルが終わる頃にフラグを下ろす
                Dispatcher.UIThread.Post(() => _isInternalScrolling = false, DispatcherPriority.Loaded);
            }
        });

        // 2. スクロール位置が変わった時の処理
        scrollViewer.ScrollChanged += (s, ev) =>
        {
            // システムによるスクロール（Extent変化による移動）なら、手動判定をスルーする
            if (_isInternalScrolling) return;

            // 垂直方向の移動がない場合は無視
//            if (Math.Abs(ev.ExtentDelta.Length) < 0.1) return;

            // 「一番下に近いか」を判定
            const double threshold = 30; // 少し余裕を持たせる
            bool isAtBottom = scrollViewer.Offset.Y >= (scrollViewer.Extent.Height - scrollViewer.Viewport.Height - threshold);

            if (!isAtBottom)
            {
                // ユーザーが手動で上に上げた
                autoScroll = false;
            }
            else
            {
                // ユーザーが自力で一番下まで戻した
                autoScroll = true;
            }
        };
        this.scrollViewer= scrollViewer;
    }


    private bool autoScroll { get; set; } = true;

    private void TestButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string? command = inputItem.TextBox.Text;
        if (command == null) return;
//        string rag = chat.GetRagText(command, "references");

        StringBuilder sb = new StringBuilder();

        sb.Append("以下の参考文献を元にユーザの質問に答えてください。\r\n");
        sb.Append("# 参考文献\r\n");
//        sb.Append(rag);
        sb.Append("\r\n");
        sb.Append("# ユーザの質問\r\n");
        sb.Append(command);

        _ = Complete(sb.ToString(), null,cancellationTokenSource.Token);
    }

    public async Task LoadMessagesAsync(string filePath)
    {
        LogFilePath = filePath;
        await LoadMessagesAsync();
    }


    public async Task LoadMessagesAsync()
    {
        if (LogFilePath == null) return;
        await chat.LoadMessagesAsync(LogFilePath);

        int items = Items.Count;
        for (int i = 0; i < items-2; i++)
        {
            Items.RemoveAt(1);
        }

        List<Microsoft.Extensions.AI.ChatMessage> chatmessages = chat.GetChatMessages();
        foreach (Microsoft.Extensions.AI.ChatMessage chatmessage in chatmessages) 
        {
            TextItem resultItem = new TextItem(chatmessage.Text);
            if (chatmessage.Role == ChatRole.System)
            {
                resultItem.TextColor = completeColor;
            }
            else if (chatmessage.Role == ChatRole.User)
            {
                resultItem.TextColor = commandColor;
            }
            else if (chatmessage.Role == ChatRole.Assistant)
            {
                resultItem.TextColor = completeColor;
            }
            else if (chatmessage.Role == ChatRole.Tool)
            {
                resultItem.TextColor = completeColor;
            }
            Items.Insert(Items.Count - 1, resultItem);
            lastResultItem = resultItem;
        }

    }

    public async Task ResetAsync()
    {
        await chat.ResetAsync();
        int itemCount = Items.Count;
        for(int i = 0; i < itemCount - 2; i++)
        {
            Items.RemoveAt(1);
        }
    }

    public async Task SaveMessagesAsync(string filePath)
    {
        LogFilePath = filePath;
        await SaveMessagesAsync();
    }
    public async Task SaveMessagesAsync()
    {
        if (LogFilePath == null) return;
        await chat.SaveMessagesAsync(LogFilePath);
    }

    public bool AutoSave { set; get; } = false;
    public string? LogFilePath { set; get; } = null;

    private async void LoadButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(async () => await LoadMessagesAsync());
        }
        catch (Exception exception)
        {
            CodeEditor2.Controller.AppendLog(exception.Message, Avalonia.Media.Colors.Red);
        }
    }

    private void ClearButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            Dispatcher.UIThread.Invoke(async () =>
            {
                await ResetAsync();
            });
        }
        catch (Exception exception)
        {
            CodeEditor2.Controller.AppendLog(exception.Message, Avalonia.Media.Colors.Red);
        }
    }

    private async void SaveButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(async()=>await SaveMessagesAsync());
        }catch(Exception exception)
        {
            CodeEditor2.Controller.AppendLog(exception.Message,Avalonia.Media.Colors.Red);
        }
    }

    private void SendButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string? command = inputItem.TextBox.Text;
        if(command == null) return;

        if(OverrideSend != null)
        {
            OverrideSend(command);
            return;
        }

        _ = Complete(command,null,cancellationTokenSource.Token);
        inputItem.TextBox.Focus();
    }

    public Action<string>? OverrideSend;

    private void TextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        if (textBox.Text == null) return;

    }

    OpenRouterChat chat;
    InputItem inputItem = new InputItem();
    TextItem? lastResultItem = null;

    private Avalonia.Media.Color commandColor = new Avalonia.Media.Color(255, 255, 200, 200);
    private Avalonia.Media.Color completeColor = new Avalonia.Media.Color(255, 200, 255, 255);

    bool inputAcceptable = true;

    private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();



    private async Task Complete(string command, IList<AITool>? tools, CancellationToken cancellation)
    {
        string? input = inputItem.TextBox.Text;
        if(input == null) return;

        // reentrant lock
        if (!inputAcceptable) return;
        inputAcceptable = false;

        try
        {
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
            try
            {
                await foreach (string ret in chat.GetAsyncCollectionChatResult(command,tools, cancellation))
                {
                    if (timerActivate & ret != "")
                    {
                        cts.Cancel();
                        await resultItem.SetText("");
                        timerActivate = false;
                    }
                    await resultItem.AppendText(ret);

                    //if (autoScroll)
                    //{
                    //    // 優先度を下げて実行し、描画を優先させる
                    //    _ = Dispatcher.UIThread.InvokeAsync(() =>
                    //    {
                    //        ListBox0.ScrollIntoView(inputItem);
                    //    }, DispatcherPriority.Background);
                    //}

                    if (autoScroll)
                    {
                        // 描画スレッドで即座にレイアウトを確定させる
                        Dispatcher.UIThread.Post(() =>
                        {
                            // 1. レイアウトを強制更新（これで新しいテキスト分の高さが確定する）
                            ListBox0.UpdateLayout();

                            // 2. 高さが確定した直後にスクロール
                            scrollViewer?.ScrollToEnd();
                        }, DispatcherPriority.Render); // Render優先度で即座に反映
                    }
                    //if (autoScroll)
                    //{
                    //    await Dispatcher.UIThread.InvokeAsync(() =>
                    //    {
                    //        ListBox0.ScrollIntoView(inputItem);
                    //    });
                    //}
                }
            }catch(Exception ex)
            {
                CodeEditor2.Controller.AppendLog(ex.Message, Avalonia.Media.Colors.Red);
            }
            stopwatch.Stop();
            await displayTimerTask;

            // change color to complete color
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                resultItem.TextColor = completeColor;
                await SaveMessagesAsync();
            });
        }catch(Exception ex)
        {
            CodeEditor2.Controller.AppendLog(ex.Message, Avalonia.Media.Colors.Red);
        }
        finally
        {
            inputAcceptable = true;
            cancellationTokenSource = new CancellationTokenSource();
        }
    }

    public async IAsyncEnumerable<string> GetAsyncCollectionChatResult(string command, IList<AITool>? tools, [EnumeratorCancellation] CancellationToken cancellation)
    {
        inputItem.TextBox.Text = command;
        await Complete(command,tools,cancellation);
        if(lastResultItem == null) yield break;
        yield return lastResultItem.Text;
    }
    public async Task<string> GetAsyncChatResult(string command, IList<AITool>? tools, CancellationToken cancellationToken)
    {
        StringBuilder sb = new StringBuilder();
        await foreach (string ret in GetAsyncCollectionChatResult(command, tools,cancellationToken))
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
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,// .Top,
            AcceptsReturn = true,
            IsReadOnly = true,
            MinHeight = 30
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
                {
                    MenuItem menuItem = new MenuItem()
                    {
                        Header = "Copy All"
                    };
                    menuItem.Click += (sender, e) =>
                    {
                        var top = TopLevel.GetTopLevel(this);
                        top?.Clipboard?.SetTextAsync(textBox.Text);
                    };
                    contextMenu.Items.Add(menuItem);
                }
                {
                    MenuItem menuItem = new MenuItem()
                    {
                        Header = "Copy"
                    };
                    menuItem.Click += (sender, e) =>
                    {
                        var top = TopLevel.GetTopLevel(this);
                        top?.Clipboard?.SetTextAsync(textBox.SelectedText);
                    };
                    contextMenu.Items.Add(menuItem);
                }
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
                ButtonBar.Children.Add(AbortButton);
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

            TextBox.KeyDown += (sender, e) =>
            {
                if (e.Key == Key.Left || e.Key == Key.Right)
                {
                    // テキストボックス内でのカーソル移動のみを許容し、
                    // 親のListBoxへのイベント伝播（フォーカス移動）を止める
                    e.Handled = true;
                }
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

        public Button AbortButton = new Button()
        {
            Content = "Abort",
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

