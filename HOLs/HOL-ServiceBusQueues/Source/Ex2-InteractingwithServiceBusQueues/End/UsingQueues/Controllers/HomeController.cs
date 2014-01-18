﻿using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using Microsoft.WindowsAzure.ServiceRuntime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using UsingQueues.Models;

namespace UsingQueues.Controllers
{
    public class HomeController : Controller
    {
        private NamespaceManager namespaceManager;
        private MessagingFactory messagingFactory;

        public HomeController()
        {
            var baseAddress = RoleEnvironment.GetConfigurationSettingValue("namespaceAddress");
            var issuerName = RoleEnvironment.GetConfigurationSettingValue("issuerName");
            var issuerKey = RoleEnvironment.GetConfigurationSettingValue("issuerKey");

            Uri namespaceAddress = ServiceBusEnvironment.CreateServiceUri("sb", baseAddress, string.Empty);

            this.namespaceManager = new NamespaceManager(namespaceAddress, TokenProvider.CreateSharedSecretTokenProvider(issuerName, issuerKey));
            this.messagingFactory = MessagingFactory.Create(namespaceAddress, TokenProvider.CreateSharedSecretTokenProvider(issuerName, issuerKey));
        }

        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public JsonResult CreateQueue(string queueName)
        {
            var queueDescription = this.namespaceManager.CreateQueue(queueName);
            return this.Json(queueDescription, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public void SendMessage(string queueName, string messageBody, bool isUrgent, bool isFollowUp)
        {
            QueueClient queueClient = this.messagingFactory.CreateQueueClient(queueName);
            var customMessage = new CustomMessage() { Date = DateTime.Now, Body = messageBody };
            BrokeredMessage bm = null;

            try
            {
                bm = new BrokeredMessage(customMessage);
                bm.Properties["Urgent"] = "1";
                bm.Properties["Priority"] = "High";
                queueClient.Send(bm);
            }
            catch { }
            finally
            {
                if (bm != null)
                {
                    bm.Dispose();
                }
            }
        }

        [HttpGet, OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
        public JsonResult RetrieveMessage(string queueName)
        {
            QueueClient queueClient = this.messagingFactory.CreateQueueClient(queueName, ReceiveMode.PeekLock);
            BrokeredMessage receivedMessage = queueClient.Receive(new TimeSpan(0, 0, 30));

            if (receivedMessage == null)
            {
                return this.Json(null, JsonRequestBehavior.AllowGet);
            }

            var receivedCustomMessage = receivedMessage.GetBody<CustomMessage>();

            var brokeredMsgProperties = new Dictionary<string, object>();
            brokeredMsgProperties.Add("Size", receivedMessage.Size);
            brokeredMsgProperties.Add("MessageId", receivedMessage.MessageId.Substring(0, 15) + "...");
            brokeredMsgProperties.Add("TimeToLive", receivedMessage.TimeToLive.TotalSeconds);
            brokeredMsgProperties.Add("EnqueuedTimeUtc", receivedMessage.EnqueuedTimeUtc.ToString("yyyy-MM-dd HH:mm:ss"));
            brokeredMsgProperties.Add("ExpiresAtUtc", receivedMessage.ExpiresAtUtc.ToString("yyyy-MM-dd HH:mm:ss"));

            var messageInfo = new
            {
                Label = receivedMessage.Label,
                Date = receivedCustomMessage.Date,
                Message = receivedCustomMessage.Body,
                Properties = receivedMessage.Properties.ToArray(),
                BrokeredMsgProperties = brokeredMsgProperties.ToArray()
            };

            receivedMessage.Complete();
            return this.Json(messageInfo, JsonRequestBehavior.AllowGet);
        }


        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
        public JsonResult Queues()
        {
            var queues = this.namespaceManager.GetQueues().Select(c => new { Name = c.Path, Messages = c.MessageCount }).ToArray();
            return this.Json(queues, JsonRequestBehavior.AllowGet);
        }

        public long GetMessageCount(string queueName)
        {
            var queueDescription = this.namespaceManager.GetQueue(queueName);
            return queueDescription.MessageCount;
        }
    }
}