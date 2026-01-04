using OllamaSharp;
using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Speech.Recognition;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Jarvix
{
    public partial class MainWindow : Window
    {
        private OllamaApiClient _ollama;
        private Chat _chat;
        private SpeechRecognitionEngine _listener;

        private readonly HttpClient _httpClient = new HttpClient();
        private readonly string _elevenApiKey = "1735ccf77bbb77b41bee2662fe194b4eade4b82f176c752a38600524f5f4ac92";
        private readonly string _voiceId = "2ssyWFy2e8RilHJEIokb";
        private readonly string _systemPrompt = "You are Jarvix. Be concise. You are talking to Evan.";

        private MediaPlayer _mediaPlayer = new MediaPlayer();
        private TaskCompletionSource<bool> _audioFinished;

        public MainWindow()
        {
            InitializeComponent();
            InitJarvix();
            SetupWakeWord();

            _mediaPlayer.MediaEnded += (s, e) => _audioFinished?.TrySetResult(true);

            // CRITICAL: This will tell us if the MP3 file is corrupted or unplayable
            _mediaPlayer.MediaFailed += (s, e) => {
                MessageBox.Show($"Audio Playback Failed: {e.ErrorException.Message}");
                _audioFinished?.TrySetResult(false);
            };
        }

        private void InitJarvix()
        {
            try
            {
                var uri = new Uri("http://localhost:11434");
                _ollama = new OllamaApiClient(uri);
                _ollama.SelectedModel = "llama3";
                _chat = new Chat(_ollama);

                this.Loaded += (s, e) => {
                    Storyboard flow = (Storyboard)this.FindResource("FlowAnimation");
                    flow.Begin();
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ollama Init Error: {ex.Message}\nMake sure Ollama is running!");
            }
        }

        private async Task SpeakWithElevenLabs(string text)
        {
            try
            {
                var url = $"https://api.elevenlabs.io/v1/text-to-speech/{_voiceId}";
                var requestData = new
                {
                    text = text,
                    model_id = "eleven_turbo_v2",
                    voice_settings = new { stability = 0.5, similarity_boost = 0.75 }
                };

                var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("xi-api-key", _elevenApiKey);

                var response = await _httpClient.PostAsync(url, content);

                if (!response.IsSuccessStatusCode)
                {
                    string errorJson = await response.Content.ReadAsStringAsync();
                    // This will tell you if you are out of credits or if the API Key is wrong
                    MessageBox.Show($"ElevenLabs API Error ({response.StatusCode}): {errorJson}");
                    return;
                }

                var audioBytes = await response.Content.ReadAsByteArrayAsync();
                string tempFile = Path.Combine(Path.GetTempPath(), "jarvix_response.mp3");

                await File.WriteAllBytesAsync(tempFile, audioBytes);

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _audioFinished = new TaskCompletionSource<bool>();
                    _mediaPlayer.Open(new Uri(tempFile));
                    _mediaPlayer.Play();
                });

                await _audioFinished.Task;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"TTS Exception: {ex.Message}");
            }
        }

        private void SetupWakeWord()
        {
            try
            {
                _listener = new SpeechRecognitionEngine(new CultureInfo("en-US"));
                Choices wakeWords = new Choices();
                wakeWords.Add(new string[] { "Jarvix", "Hey Jarvix" });

                _listener.LoadGrammar(new Grammar(new GrammarBuilder(wakeWords)));
                _listener.SpeechRecognized += async (s, e) => {
                    if (e.Result.Confidence > 0.5)
                    {
                        await AskJarvis("I am here, sir.");
                    }
                };

                _listener.SetInputToDefaultAudioDevice();
                _listener.RecognizeAsync(RecognizeMode.Multiple);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Mic/Speech Error: {ex.Message}\nCheck your microphone settings.");
            }
        }

        private async Task AskJarvis(string prompt)
        {
            this.Dispatcher.Invoke(() => this.Visibility = Visibility.Visible);
            Storyboard talking = (Storyboard)this.FindResource("TalkingEffect");
            talking.Begin();

            try
            {
                StatusText.Text = "pondering...";
                string responseText = "";

                // Using the Chat object for memory
                await foreach (var res in _chat.SendAsync(prompt))
                {
                    responseText += res;
                }

                if (string.IsNullOrEmpty(responseText))
                {
                    MessageBox.Show("Ollama returned an empty response. Is llama3 loaded?");
                }

                StatusText.Text = "speaking...";
                await SpeakWithElevenLabs(responseText);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Brain Error (Ollama): {ex.Message}");
            }
            finally
            {
                talking.Stop();
                StatusText.Text = "JARVIX ONLINE";
                await Task.Delay(1500);
                this.Dispatcher.Invoke(() => this.Visibility = Visibility.Hidden);
            }
        }

        protected override async void OnMouseDoubleClick(MouseButtonEventArgs e)
        {
            await AskJarvis("System check.");
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) this.DragMove();
        }
    }
}