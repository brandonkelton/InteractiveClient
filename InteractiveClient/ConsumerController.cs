using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace InteractiveClient
{
    public class ConsumerController
    {
        public static int MaxConsumers = 100;
        public ConcurrentDictionary<Guid, Client> LinkedClients = new ConcurrentDictionary<Guid, Client>();
        public ConcurrentBag<Word> Words = new ConcurrentBag<Word>();
        public double CurrentBufferLevel { get; private set; }
        public int ConsumerCount => LinkedClients.Count();

        private Client _client;
        private string _clientId = String.Empty;
        private ConcurrentDictionary<Guid, Thread> _threads = new ConcurrentDictionary<Guid, Thread>();
        private long _stopRequested = 0;
        private long _consumerWatcherStopRequested = 0;
        private Thread _consumerWatcherThread = null;
        private object _lockObj = new object();
        //private ManualResetEvent _consume = new ManualResetEvent(false);
        //private ManualResetEvent _stoppingConsumer = new ManualResetEvent(true);

        public ConsumerController(Client client)
        {
            _client = client;
            _clientId = GetClientId();
        }

        public void StartSelfAdjustingConsumers()
        {
            if (_consumerWatcherThread == null)
            {
                _consumerWatcherThread = new Thread(new ThreadStart(() => StartConsumers()));
                _consumerWatcherThread.Start();
            }
        }

        // This is the governor of the self-adjusting consumers.
        // In order to adjust, this runs on it's own thread and monitors
        // the buffer level as provided by each individual word when that word
        // was extracted from the buffer on the server-side.
        private void StartConsumers()
        {
            StartConsumers(1);

            while (true)
            {
                //if (LinkedClients.All(c => !c.IsActive))
                //    StopAllConsumers();

                if (Interlocked.Read(ref _consumerWatcherStopRequested) == 1)
                {
                    break;
                }

                if (CurrentBufferLevel >= 90 && ConsumerCount < MaxConsumers)
                {
                    StartConsumers(1);
                }
                else if (CurrentBufferLevel <= 30 && ConsumerCount > 1)
                {
                    StopConsumers(1);
                }

                Thread.Sleep(500);
            }
        }

        public void StartConsumers(int count)
        {
            Interlocked.Exchange(ref _stopRequested, 0);

            if (!_client.IsActive || String.IsNullOrEmpty(_clientId))
            {
                Console.WriteLine("CLIENT IS NOT CONNECTED");
                return;
            }

            for (int i=0; i<count; i++)
            {
                var client = ConnectAndLinkClient();
                if (client != null)
                {
                    var thread = new Thread(new ThreadStart(() => ExecuteConsumer(client)));

                    int attempts = 0;
                    const int MAX_ATTEMPTS = 100;
                    bool success = false;
                    while (attempts++ < MAX_ATTEMPTS && !(success = _threads.TryAdd(client.LocalId, thread)))
                    {
                        Thread.Sleep(10);
                    }
                    if (success) thread.Start();
                }
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

            int attempts = 0;
            const int MAX_ATTEMPTS = 100;
            bool success = false;
            while (attempts++ < MAX_ATTEMPTS && !(success = LinkedClients.TryAdd(client.LocalId, client)))
            {
                Thread.Sleep(10);
            }

            return success ? client : null;
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
            while (client.IsActive)
            {
                if (Interlocked.Read(ref _stopRequested) == 1)
                {
                    break;
                }

                //_consume.Reset();
                //_stoppingConsumer.WaitOne();

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
                
                lock (_lockObj)
                {
                    CurrentBufferLevel = word.BufferLevel;
                }

                Words.Add(word);

                //_consume.Set();
            }

            client.Kill();
        }

        public void StopConsumers(int count)
        {
            var stopConsumerCount = count > LinkedClients.Count ? LinkedClients.Count : count;
            for (int i=0; i<stopConsumerCount; i++)
            {
                var consumerKey = LinkedClients.Keys.FirstOrDefault();

                var attempts = 0;
                const int MAX_ATTEMPTS = 100;
                Client removeClient = null;
                bool success = false;
                while (attempts++ < MAX_ATTEMPTS && !(success = LinkedClients.TryRemove(consumerKey, out removeClient)))
                {
                    Thread.Sleep(10);
                }
                StopConsumer(removeClient);                
            }
        }

        private void StopConsumer(Client consumer)
        {
            if (consumer != null)
            {
                consumer.Stop();

                //_consume.WaitOne();
                //_stoppingConsumer.Reset();

                var unlinkMessenger = new Messenger();
                consumer.Send($"unlinkfrom {_clientId}", unlinkMessenger);
                consumer.Receive(unlinkMessenger);

                var disconnectMessenger = new Messenger();
                consumer.Send("disconnect", disconnectMessenger);
                consumer.Receive(disconnectMessenger);

                //_stoppingConsumer.Set();
                //_consume.Reset();
                
                consumer.Kill();

                int attempts = 0;
                const int MAX_ATTEMPTS = 100;
                bool success = false;
                Thread thread = null;
                while (attempts++ < MAX_ATTEMPTS && !(success = _threads.TryRemove(consumer.LocalId, out thread)))
                {
                    Thread.Sleep(10);
                }

                if (thread != null) thread.Join();
            }
        }

        public void StopAllConsumers()
        {
            Interlocked.Exchange(ref _consumerWatcherStopRequested, 1);
            Interlocked.Exchange(ref _stopRequested, 1);

            if (_consumerWatcherThread != null)
            {
                _consumerWatcherThread.Join();
            }

            while (LinkedClients.Count() > 0)
            {
                StopConsumers(LinkedClients.Count());
            }
        }
    }
}
