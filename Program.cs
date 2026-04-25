namespace Client
{
    using System.Text;

    class Program
    {
        static async Task Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            using var client = new HttpClient();
            var serverUrl = "https://uniquename3342.onrender.com/send";
            var cts = new CancellationTokenSource();

            _ = Task.Run(() =>
            {
                while (true)
                {
                    Console.WriteLine("Введіть число (або 'exit' для виходу): ");
                    var input = Console.ReadLine();
                    if (input?.ToLower() == "exit")
                    {
                        cts.Cancel();
                        break;
                    }

                    _ = SendNumberAsync(client, serverUrl, input);
                }
            });

            while (!cts.Token.IsCancellationRequested)
            {
                await Task.Delay(500);
            }
        }

        static async Task SendNumberAsync(HttpClient client, string url, string number)
        {
            try
            {
                var content = new StringContent(number, Encoding.UTF8, "text/plain");
                var response = await client.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    Console.Title = "Сервер відповів: " + result;
                }
                else
                {
                    Console.WriteLine($"Помилка {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка відправлення: {ex.Message}");
            }
        }
    }
}
