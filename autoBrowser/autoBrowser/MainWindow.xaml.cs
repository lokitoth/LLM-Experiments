using Azure.AI.OpenAI;
using Microsoft.Web.WebView2.Core;
//using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace autoBrowser
{
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window
  {
    private OpenAIClient client;
    private ObservableCollection<ChatMessage> messages;

    public MainWindow()
    {
      this.InitializeComponent();
      
      this.client = new OpenAIClient(KeyConstants.OpenAIKey);
      this.messages = new ObservableCollection<ChatMessage>();

      this.ChatOutput.ItemsSource = this.messages;

      this.WebView.NavigationStarting += this.WebView_NavigationStarting;
      this.WebView.NavigationCompleted += this.WebView_NavigationCompleted;

    }

    private void WebView_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
      this.StopRefreshButton.Content = "Stop";
      this.UpdateHistoryNavigationButtons();
      this.AddressText.Text = this.WebView.Source.ToString();
    }

    private void WebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
      this.StopRefreshButton.Content = "Refresh";
      this.UpdateHistoryNavigationButtons();
    }

    private void UpdateHistoryNavigationButtons()
    {
      this.BackButton.IsEnabled = this.WebView.CanGoBack;
      this.ForwardButton.IsEnabled = this.WebView.CanGoForward;
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
      if (!this.WebView.CanGoBack)
      {
        this.UpdateHistoryNavigationButtons();
        return;
      }

      this.WebView.GoBack();
      e.Handled = true;
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
      if (!this.WebView.CanGoForward)
      {
        this.UpdateHistoryNavigationButtons();
        return;
      }

      this.WebView.GoForward();
      e.Handled= true;
    }

    private void StopRefreshButton_Click(object sender, RoutedEventArgs e)
    {
      string? maybeCommand = this.StopRefreshButton.Content as string;
      if (maybeCommand == null)
      {
        return;
      }

      switch (maybeCommand.ToLowerInvariant().Trim())
      {
        case "stop":
          this.WebView.Stop();
          break;
        case "refresh":
          this.WebView.Reload();
          break;
      }

      e.Handled = true;
    }

    private void TryNavigate()
    {
      string maybeUriString = this.AddressText.Text;

      if (!maybeUriString.StartsWith("http:") &&
          !maybeUriString.StartsWith("https:") &&
          !maybeUriString.StartsWith("file:"))
      {
        maybeUriString = "https://" + maybeUriString;
      }

      UriBuilder builder = new UriBuilder(maybeUriString);
      if (builder.Scheme == null || !maybeUriString.ToLowerInvariant().StartsWith(builder.Scheme.ToLowerInvariant()))
      {
        builder.Scheme = "https";
        builder.Port = 443;
      }

      this.WebView.Source = builder.Uri;
    }

    private void GoButton_Click(object sender, RoutedEventArgs e)
    {
      this.TryNavigate();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
    }

    private void TextBox_KeyUp(object sender, KeyEventArgs e)
    {
    }

    private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
    }

    private void AddressText_PreviewKeyUp(object sender, KeyEventArgs e)
    {
      if (e.Key == Key.Enter)
      {
        this.TryNavigate();
        e.Handled = true;
      }
    }

    bool justGotFocus = true;
    private void AddressText_GotFocus(object sender, RoutedEventArgs e)
    {
      this.AddressText.SelectAll();
    }

    private void AddressText_MouseUp(object sender, MouseButtonEventArgs e)
    {
    }

    private void AddressText_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
      if (!this.AddressText.IsKeyboardFocusWithin)
      {
        this.AddressText.Focus();
        e.Handled = true;
      }
    }

    private void AppendChat(ChatMessage message)
    {
      this.messages.Add(message);
    }

    private void TrySubmitChat()
    {
      string? maybeChatText = this.ChatInputText.Text?.Trim();
      if (String.IsNullOrWhiteSpace(maybeChatText)) return;

      this.AppendChat(new ChatMessage(ChatRole.User, maybeChatText));
      _ = this.InvokeLLMAsync(this.ChatInputText); // TODO: "Thinking" spinner

      this.ChatInputText.Text = string.Empty;
    }

    private async Task<string> GetInnerTextAsync()
    {
      string sHtml = await this.WebView.CoreWebView2.ExecuteScriptAsync("document.documentElement.innerText");  
      string sHtmlDecoded = System.Text.RegularExpressions.Regex.Unescape(sHtml);

      return sHtmlDecoded;
    }

    private async Task InvokeLLMAsync(TextBox chatInputText)
    {
      string pageText = await GetInnerTextAsync();
      const int MAX_INPUT_LENGTH = 4000;
      if (pageText.Length > MAX_INPUT_LENGTH)
      {
        pageText = pageText.Substring(0, MAX_INPUT_LENGTH);
      }

      ChatCompletionsOptions completionsOptions = new ChatCompletionsOptions()
      {
        ChoicesPerPrompt = 1
      };

      completionsOptions.Messages.Add(new ChatMessage(ChatRole.System, 
$@"The following conversation is happening in the context of a web page. Here is the text in the page:

```
${pageText}
```

Please answer queries with this context.
"));

      foreach (var message in this.messages)
      {
        // TODO: compute the max tokens appropriately
        if (message.Role == ChatRole.System) continue;

        completionsOptions.Messages.Add(message);
      }

      try
      {
        Azure.Response<ChatCompletions> result = await this.client.GetChatCompletionsAsync("gpt-3.5-turbo", completionsOptions);
        ChatChoice choice = result.Value.Choices[0];
        this.messages.Add(choice.Message);
      }
      catch (Exception e)
      {
        this.messages.Add(new ChatMessage(ChatRole.System, e.Message));
      }
    }

    private void ChatInputText_PreviewKeyDown(object sender, KeyEventArgs e)
    {
      if (e.Key == Key.Enter)
      {
        this.TrySubmitChat();
        e.Handled = !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
      }
    }
  }
}
