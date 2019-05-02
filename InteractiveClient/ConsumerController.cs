using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;

namespace InteractiveClient
{
    public class ConsumerController
    {
        public List<Client> LinkedClients = new List<Client>();
        public ConcurrentBag<Word> Words = new ConcurrentBag<Word>();

        private Client _client;
        private string _clientId = String.Empty;
        private Dictionary<Guid, Thread> _threads = new Dictionary<Guid, Thread>();
        private long _stopRequested = 0;

        public ConsumerController(Client client)
        {
            _client = client;
            _clientId = GetClientId();
        }

        public void StartConsumers(int count)
        {
            if (!_client.IsActive || String.IsNullOrEmpty(_clientId))
            {
                Console.WriteLine("CLIENT IS NOT CONNECTED");
                return;
            }

            for (int i=0; i<count; i++)
            {
                var client = ConnectAndLinkClient();
                var thread = new Thread(new ThreadStart(() => ExecuteConsumer(client)));
                _threads.Add(client.LocalId, thread);
                thread.Start();
            }
        }

        private Client ConnectAndLinkClient()
        {
            var client = new Client();

            var messenger = new Messenger();
            client.Connect(_client.Endpoint.Address.ToString(), _client.Endpoint.Port, messenger);
            if (messenger.IsError)
            {
                Console.WriteLine($"\n{messenger.Message.ToString()}\n");
                client.Kill();
                return null;
            }
            messenger.ClearMessage();

            client.Send($"linkto {_clientId}", messenger);
            if (messenger.IsError)
            {
                Console.WriteLine($"\n{messenger.Message.ToString()}\n");
                client.Kill();
                return null;
            }
            messenger.ClearMessage();
            client.Receive(messenger);

            if (messenger.Message.ToString() != "LINKED TO CLIENT")
            {
                Console.WriteLine($"\n{messenger.Message.ToString()}\n");
                client.Kill();
                return null;
            }

            LinkedClients.Add(client);

            return client;
        }

        private string GetClientId()
        {
            var messenger = new Messenger();
            _client.Send("id", messenger);

            if (messenger.IsError)
            {
                return null;
            }

            messenger.ClearMessage();

            _client.Receive(messenger);

            if (messenger.IsError)
            {
                return null;
            }

            Guid clientId;
            if (Guid.TryParse(messenger.Message.ToString(), out clientId))
            {
                return clientId.ToString();
            }

            return null;
        }

        private void ExecuteConsumer(Client client)
        {
            while (true)
            {
                if (Interlocked.Read(ref _stopRequested) == 1)
                {
                    break;
                }

                if (Interlocked.Read(ref client.StopRequested) == 1)
                {
                    break;
                }

                var messenger = new Messenger();

                client.Send("getword", messenger);
                if (messenger.IsError)
                {
                    break;
                }
                messenger.ClearMessage();

                client.Receive(messenger);
                if (messenger.IsError)
                {
                    break;
                }

                var word = JsonConvert.DeserializeObject<Word>(messenger.Message.ToString());
                if (word == null || word.Text == "<EOF>")
                {
                    break;
                }
                Words.Add(word);

                if (word.BufferLevel >= 8)
                {
                    Thread.Sleep(100);
                }
            }

            client.Kill();
        }

        public void StopConsumers(int count)
        {
            var stopConsumerCount = count > LinkedClients.Count ? LinkedClients.Count : count;
            for (int i=0; i<stopConsumerCount; i++)
            {
                var consumer = LinkedClients[i];
                Interlocked.Exchange(ref consumer.StopRequested, 1);

                var thread = _threads.GetValueOrDefault(consumer.LocalId);
                if (thread != null)
                {
                    thread.Join();
                    _threads.Remove(consumer.LocalId);
                }
            }
        }

        public void StopAllConsumers()
        {
            Interlocked.Exchange(ref _stopRequested, 1);

            foreach (var client in LinkedClients)
            {
                client.Kill();
            }

            foreach (var thread in _threads.Values)
            {
                thread.Join();
            }
        }
    }
}
