﻿using Proto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BaileysCSharp.Core.Models;
using static Proto.Message.Types;
using BaileysCSharp.Core.Types;

namespace BaileysCSharp.Core.Utils
{
    public class HistoryUtil
    {
        public static HistorySyncNotification GetHistoryMsg(Message message)
        {
            var normalizedContent = message != null ? MessageUtil.NormalizeMessageContent(message) : null;
            var anyHistoryMsg = normalizedContent?.ProtocolMessage?.HistorySyncNotification;
            return anyHistoryMsg;

        }


        public static async Task<MessageHistoryModel> DownloadAndProcessHistorySyncNotification(Message.Types.HistorySyncNotification msg)
        {
            var historyMsg = await DownloadHistory(msg);
            return ProcessHistoryMessage(historyMsg);
        }

        private static MessageHistoryModel ProcessHistoryMessage(HistorySync item)
        {
            List<WebMessageInfo> messages = new List<WebMessageInfo>();
            List<ContactModel> contacts = new List<ContactModel>();
            List<ChatModel> chats = new List<ChatModel>();

            switch (item.SyncType)
            {
                case HistorySync.Types.HistorySyncType.InitialBootstrap:
                case HistorySync.Types.HistorySyncType.Recent:
                case HistorySync.Types.HistorySyncType.Full:

                    foreach (var conv in item.Conversations)
                    {
                        contacts.Add(new ContactModel()
                        {
                            ID = conv.Id,
                            Name = conv.Name,
                        });

                        var chat = chats.FirstOrDefault(x => x.ID == conv.Id) ?? new ChatModel() { ID = conv.Id, Archived = conv.Archived, ConversationTimestamp = conv.ConversationTimestamp, UnreadCount = conv.UnreadCount, ReadOnly = conv.ReadOnly };

                        var msgs = conv.Messages;
                        foreach (var msg in msgs)
                        {
                            var message = msg.Message;
                            messages.Add(message);
                            if (!message.Key.FromMe && message.MessageTimestamp > chat.LastMessageRecvTimestamp)
                            {
                                chat.LastMessageRecvTimestamp = message.MessageTimestamp;
                            }

                            if (message.MessageStubType == WebMessageInfo.Types.StubType.BizPrivacyModeToBsp || message.MessageStubType == WebMessageInfo.Types.StubType.BizPrivacyModeToFb && message.MessageStubParameters.Count > 0)
                            {
                                contacts.Add(new ContactModel()
                                {
                                    ID = string.IsNullOrEmpty(message.Key.Participant) ? message.Key.RemoteJid : message.Key.Participant,
                                    VerifiedName = message.MessageStubParameters[0]
                                });
                            }
                        }
                        if (JidUtils.IsJidUser(chat.ID) && chat.ReadOnly && chat.Archived)
                        {
                            chat.ReadOnly = false;
                        }
                        if (!chats.Contains(chat))
                        {
                            chats.Add(chat);
                        }
                    }


                    break;


                case HistorySync.Types.HistorySyncType.PushName:
                    foreach (var c in item.Pushnames)
                    {
                        contacts.Add(new ContactModel() { ID = c.Id, Notify = c.Pushname_ });
                    }
                    break;

                default:
                    break;
            }
            return new MessageHistoryModel(contacts, chats, messages,false);
        }

        private static async Task<HistorySync> DownloadHistory(HistorySyncNotification msg)
        {
            var stream = await MediaMessageUtil.DownloadContentFromMessage(msg, "md-msg-hist", new MediaDownloadOptions());
            var buffer = BufferReader.Inflate(stream);
            var syncData = HistorySync.Parser.ParseFrom(buffer);
            return syncData;
        }

    }
}
