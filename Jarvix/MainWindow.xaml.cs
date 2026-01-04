using System;
using System.Speech.Synthesis;
using System.Windows;
using System.Windows.Input;
using System.Threading.Tasks;
using OllamaSharp;
using System.Windows.Media.Animation;

namespace Jarvix
{
    public partial class MainWindow : Window
    {
        private OllamaApiClient _ollama;
        private string _systemPrompt = "You are Jarvis. Be concise and professional.";

        public MainWindow()
        {
            InitializeComponent();

            this.Loaded += (s, e) =>
            {
                Storyboard flow = (Storyboard)this.FindResource("FlowAnimation");
                flow.Begin();
            };
            var uri = new Uri("http://localhost:11434");
            _ollama = new OllamaApiClient(uri);
            _ollama.SelectedModel = "llama3";
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }


        private async Task AskJarvis(string prompt)
        {
            Storyboard flow = (Storyboard)this.FindResource("FlowAnimation");
            Storyboard talking = (Storyboard)this.FindResource("TalkingEffect");
            flow.SetSpeedRatio(5.0);
            talking.Begin();

            StatusText.Text = "THINKING...";

            string response = "";
            await foreach (var res in _ollama.GenerateAsync(_systemPrompt + " " + prompt))
            {
                response += res.Response;
            }

            talking.Stop();
            flow.SetSpeedRatio(1.0);
            StatusText.Text = "JARVIS ONLINE";
            MessageBox.Show(response);
        }

        protected override async void OnMouseDoubleClick(MouseButtonEventArgs e)
        {
            await AskJarvis("System check: Are you operational?");
        }
    }
}