using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace CodeEditor2AiPlugin;

public partial class ChatWindow : UserControl
{
    public ChatWindow()
    {
        InitializeComponent();
    }
    /*
using System.ClientModel;
using OpenAI.Chat;
using System;
using System.Net;

namespace ChatClient
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private string apiKey;

        private async void Form1_Load(object sender, EventArgs e)
        {
            apiKey = Microsoft.VisualBasic.Interaction.InputBox(
                "input API Key",
                "API Key",
                ""
                );
            OpenAI.OpenAIClientOptions openAIClientOptions = new OpenAI.OpenAIClientOptions()
            { Endpoint = new System.Uri("https://openrouter.ai/api/v1") };

            client = new OpenAI.Chat.ChatClient(
                model: "deepseek/deepseek-r1:free",
                new ApiKeyCredential(apiKey),
                openAIClientOptions
                );

        }

        private OpenAI.Chat.ChatClient client;
        private async void sendButton_Click(object sender, EventArgs e)
        {
            sendButton.Text = "sent";

            AsyncCollectionResult<StreamingChatCompletionUpdate> completionUpdates
                = client.CompleteChatStreamingAsync(userTextBox.Text);

            await foreach (StreamingChatCompletionUpdate completionUpdate in completionUpdates)
            {
                if (completionUpdate.ContentUpdate.Count > 0)
                {
                    //Console.Write(completionUpdate.ContentUpdate[0].Text);
                    assistantTextBox.Text += completionUpdate.ContentUpdate[0].Text;
                }
            }
            sendButton.Text = "send";
        }
     
     
     
     */
}