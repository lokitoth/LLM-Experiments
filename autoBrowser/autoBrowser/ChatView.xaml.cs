using Azure.AI.OpenAI;
using Microsoft.DeepDev;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
  public static class ModelConstants
  {
    public const string FastModel = "gpt-3.5-turbo";
    public const int    FastModelTokenLimit = 4096;
    public static readonly ITokenizer 
                        FastModelTokenizer = TokenizerBuilder.CreateByModelName(FastModel);
  }

  public class MeasuredChatMessage : ChatMessage
  {
    private readonly IReadOnlyCollection<string> EMPTY = new List<string>();

    public MeasuredChatMessage(ChatRole role, string content) : base(role, content)
    {
      this.NumTokens = ModelConstants.FastModelTokenizer.Encode(content, EMPTY).Count + 2 /* start/end */ + 1 /* role; TODO: Once name is supported, we need to subtract this one. */;
    }

    public MeasuredChatMessage(ChatMessage chatMessage) : this(chatMessage.Role, chatMessage.Content)
    {
    }

    public int NumTokens { get; private set; }
  }

  public class Conversation
  {
    public ObservableCollection<ChatTask> Tasks { get; private set; } = new ObservableCollection<ChatTask>();

    public List<MeasuredChatMessage> History { get; private set; } = new List<MeasuredChatMessage>();

    public ChatTask CreateTask(string userMessage)
    {
      MeasuredChatMessage chatMessage = new MeasuredChatMessage(ChatRole.User, userMessage);

      int index = this.History.Count;
      this.History.Add(chatMessage);

      return new ChatTask(this, index);
    }
  }

  public class ChatTask
  {
    private int userMessageIndex;

    public ChatTask(Conversation parent, int userMessageIndex)
    {
      this.userMessageIndex = userMessageIndex;

      this.Parent = parent;
    }

    public static MeasuredChatMessage SystemMessage =>
      new MeasuredChatMessage(ChatRole.System, 
@""
        );

    public MeasuredChatMessage UserMessage => this.Parent.History[this.userMessageIndex];

    public ObservableCollection<MeasuredChatMessage> ThoughtProcess = new ObservableCollection<MeasuredChatMessage>();

    

    public MeasuredChatMessage AssistantFinalResponse
    {
      get;
      set;
    }

    public Conversation Parent { get; private set; }
  }

  public class TaskAgent
  {
    public TaskAgent() 
    { 
    }
  }


  /// <summary>
  /// Interaction logic for ChatView.xaml
  /// </summary>
  public partial class ChatView : UserControl
  {
    private static VolatileMemoryStore memoryStore;
    private static IKernel kernel;
    private Conversation conversation;

    private TaskAgent RunningAgent { get; set; } 
    
    static ChatView()
    {
      memoryStore = new VolatileMemoryStore(); // TODO: Save/Load

      kernel = Kernel.Builder
            .WithMemoryStorage(memoryStore)
            .Configure(config =>
            {
              const string key = KeyConstants.OpenAIKey;
              config
                .AddOpenAIChatCompletionService(ModelConstants.FastModel, ModelConstants.FastModel, key)
                .AddOpenAITextEmbeddingGenerationService(ModelConstants.FastModel, ModelConstants.FastModel, key);
            })
            .Build();
    }

    public ChatView()
    {
      InitializeComponent();

      
    }
  }
}
