﻿/** @file NIMTalkAPI.cs
  * @brief NIM SDK提供的talk接口
  * @copyright (c) 2015, NetEase Inc. All rights reserved
  * @author Harrison
  * @date 2015/12/8
  */

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
#if UNITY
using UnityEngine;
using MonoPInvokeCallbackAttribute = AOT.MonoPInvokeCallbackAttribute;
#endif

namespace NIM
{
    public delegate void ReportUploadProgressDelegate(long uploadedSize, long totalSize,object progressData);

    public delegate void ReceiveBatchMesaagesDelegate(List<NIMReceivedMessage> msgsList);

    public delegate bool TeamNotificationFilterDelegate(NIMIMMessage msg, string jsonExtension);

    public delegate void RecallMessageDelegate(ResponseCode result, RecallNotification[] notify);

    /// <summary>
    /// nos 上传进度回调
    /// </summary>
    public class NimUploadProgressData
    {
        public NIMIMMessage Message { get; set; }

        public ReportUploadProgressDelegate ProgressAction { get; set; }
    }

    /// <summary>
    /// 消息相关api
    /// </summary>
    public class TalkAPI
    {
        private static IMReceiveMessageCallback _receivedMessageCallback;
        private static IMMessageArcCallback _messageArcCallback;
        private static UploadFileCallback _uploadFileProgressChanged;
        /// <summary>
        /// 接收消息事件通知
        /// </summary>
        public static EventHandler<NIMReceiveMessageEventArgs> OnReceiveMessageHandler { get; set; }

        /// <summary>
        /// 发送消息结果通知
        /// </summary>
        public static EventHandler<MessageArcEventArgs> OnSendMessageCompleted { get; set; }

        internal static void RegisterCallbacks()
        {
            _receivedMessageCallback = new IMReceiveMessageCallback(OnReceiveIMMessage);
            _messageArcCallback = new IMMessageArcCallback(OnReceiveMessageAck);
            _uploadFileProgressChanged = new UploadFileCallback(OnUploadFileProgressChanged);
            TalkNativeMethods.nim_talk_reg_ack_cb("", _messageArcCallback, IntPtr.Zero);
            TalkNativeMethods.nim_talk_reg_receive_cb("", _receivedMessageCallback, IntPtr.Zero);
        }

        /// <summary>
        /// 发送消息
        /// </summary>
        /// <param name="message">消息对象</param>
        /// <param name="action">文件类消息附件上传进度</param>
        public static void SendMessage(NIMIMMessage message, ReportUploadProgressDelegate action = null)
        {
            System.Diagnostics.Debug.Assert(message != null && !string.IsNullOrEmpty(message.ReceiverID));
            var msg = message.Serialize();
            IntPtr ptr = IntPtr.Zero;
            if (action != null)
            {
                NimUploadProgressData data = new NimUploadProgressData();
                data.Message = message;
                data.ProgressAction = action;
                ptr = NimUtility.DelegateConverter.ConvertToIntPtr(data);
            }
            TalkNativeMethods.nim_talk_send_msg(msg, null, _uploadFileProgressChanged, ptr);
        }

        /// <summary>
        /// 向群组强制推送消息
        /// </summary>
        /// <param name="message">消息对象</param>
        /// <param name="forceMsg">强制推送内容</param>
        /// <param name="action">文件类消息附件上传进度</param>
        public static void SendTeamFrocePushMessage(NIMIMMessage message, TeamForecePushMessage forceMsg, ReportUploadProgressDelegate action = null)
        {
            System.Diagnostics.Debug.Assert(message != null && message.SessionType == Session.NIMSessionType.kNIMSessionTypeTeam);
            var msgJson = forceMsg.Serialize(message);
            IntPtr ptr = IntPtr.Zero;
            if (action != null)
            {
                NimUploadProgressData data = new NimUploadProgressData();
                data.Message = message;
                data.ProgressAction = action;
                ptr = NimUtility.DelegateConverter.ConvertToIntPtr(data);
            }
            TalkNativeMethods.nim_talk_send_msg(msgJson, null, _uploadFileProgressChanged, ptr);
        }

        /// <summary>
        /// 取消发送消息,目前用于取消发送文件消息
        /// </summary>
        /// <param name="message">消息体</param>
        /// <param name="action">附件上传进度回调</param>
        public static void StopSendMessage(NIMIMMessage message, ReportUploadProgressDelegate action = null)
        {
            var msg = message.Serialize();
            IntPtr ptr = IntPtr.Zero;
            if (action != null)
            {
                NimUploadProgressData data = new NimUploadProgressData();
                data.Message = message;
                data.ProgressAction = action;
                ptr = NimUtility.DelegateConverter.ConvertToIntPtr(data);
            }
            TalkNativeMethods.nim_talk_stop_send_msg(msg, null, _uploadFileProgressChanged, ptr);
        }

        /// <summary>
        /// 注册接收群通知是否需要过滤的回调
        /// </summary>
        /// <param name="action"></param>
        public static void RegTeamNotificationFilterCb(TeamNotificationFilterDelegate action)
        {
            IntPtr ptr = IntPtr.Zero;
            if (action != null)
                ptr = NimUtility.DelegateConverter.ConvertToIntPtr(action);
            TalkNativeMethods.nim_talk_reg_notification_filter_cb(null, TeamNotificationFilter, ptr);
        }

        /// <summary>
        /// 由其他消息生成转发消息
        /// </summary>
        /// <param name="srcMsg">原始消息</param>
        /// <param name="msgSetting">新的消息属性</param>
        /// <param name="msgId">新的客户端消息id</param>
        /// <param name="sessionId">转发目标</param>
        /// <param name="sessionType">转发目标会话类型</param>
        /// <param name="timetag">消息时间</param>
        /// <returns></returns>
        public static NIMIMMessage CreateRetweetMessage(NIMIMMessage srcMsg, NIMMessageSetting msgSetting, string msgId, string sessionId, Session.NIMSessionType sessionType, long timetag)
        {
            string srcMsgJson = srcMsg.Serialize();
            string settingJson = string.Empty;
            if (msgSetting != null)
            {
                settingJson = msgSetting.Serialize();
            }
            var newMsgPtr = TalkNativeMethods.nim_talk_create_retweet_msg(srcMsgJson, msgId, sessionType, sessionId, settingJson, timetag);
            if (newMsgPtr != IntPtr.Zero)
            {
                NimUtility.Utf8StringMarshaler marshaler = new NimUtility.Utf8StringMarshaler();
                var newMsg = marshaler.MarshalNativeToManaged(newMsgPtr);
                var newMsgJson = newMsg.ToString();
                var dstMsg = MessageFactory.CreateMessage(newMsgJson);
                GlobalAPI.FreeStringBuffer(newMsgPtr);
                return dstMsg;
            }
            return null;
        }

		private static readonly NIMTeamNotificationFilterFunc TeamNotificationFilter = OnTeamNotificationFilter;
		[MonoPInvokeCallback(typeof(NIMTeamNotificationFilterFunc))]
		static bool OnTeamNotificationFilter (string content, string jsonExt, IntPtr ptr)
        {
            if (ptr != IntPtr.Zero)
            {
                var msg = MessageFactory.CreateMessage(content);
				var action = NimUtility.DelegateConverter.ConvertFromIntPtr<TeamNotificationFilterDelegate> (ptr);
                return action(msg, jsonExt);
            }
            return false;
        }

		[MonoPInvokeCallback(typeof(UploadFileCallback))]
        private static void OnUploadFileProgressChanged(long uploadedSize, long totalSize, string jsonExtension, IntPtr userData)
        {
            var data = NimUtility.DelegateConverter.ConvertFromIntPtr(userData) as NimUploadProgressData;
            if(data != null && data.ProgressAction != null)
            {
                data.ProgressAction(uploadedSize, totalSize, data.Message);
            }
            //NimUtility.DelegateConverter.Invoke<ReportUploadProgressDelegate>(userData, uploadedSize, totalSize);
        }
		[MonoPInvokeCallback(typeof(IMMessageArcCallback))]
        private static void OnReceiveMessageAck(string arcResult, IntPtr userData)
        {
            if (string.IsNullOrEmpty(arcResult))
                return;
            var arc = MessageAck.Deserialize(arcResult);
            if (OnSendMessageCompleted != null)
            {
                OnSendMessageCompleted(null, new MessageArcEventArgs(arc));
            }
        }
		[MonoPInvokeCallback(typeof(IMReceiveMessageCallback))]
        static void OnReceiveIMMessage(string content, string jsonArcResult, IntPtr userData)
        {
            if (string.IsNullOrEmpty(content) || OnReceiveMessageHandler == null)
                return;
            System.Diagnostics.Debug.WriteLine("receive message:" + content);
            var obj = Newtonsoft.Json.Linq.JObject.Parse(content);
            NIMReceivedMessage msg = new NIMReceivedMessage();
            var resCode = obj.SelectToken(NIMReceivedMessage.ResCodePath);
            var feature = obj.SelectToken(NIMReceivedMessage.FeaturePath);
            var token = obj.SelectToken(NIMReceivedMessage.MessageContentPath);
            if (resCode != null)
                msg.ResponseCode = resCode.ToObject<ResponseCode>();
            if (feature != null)
                msg.Feature = feature.ToObject<NIMMessageFeature>();

            if (token != null && token.Type == Newtonsoft.Json.Linq.JTokenType.Object)
            {
                var contentObj = token.ToObject<Newtonsoft.Json.Linq.JObject>();
                var realMsg = MessageFactory.CreateMessage(contentObj);
                msg.MessageContent = realMsg;
                OnReceiveMessageHandler(null, new NIMReceiveMessageEventArgs(msg));
            }
        }

        /// <summary>
        /// 注册接收批量消息回调 （如果在注册了接收消息回调的同时也注册了该批量接口，当有批量消息时，会改走这个接口通知应用层，例如登录后接收到的离线消息等）
        /// </summary>
        /// <param name="cb"></param>
        public static void RegReceiveBatchMessagesCb(ReceiveBatchMesaagesDelegate cb)
        {
            var ptr = NimUtility.DelegateConverter.ConvertToIntPtr(cb);
            TalkNativeMethods.nim_talk_reg_receive_msgs_cb(null, OnReceivedBatchMessages, ptr);
        }

		private static readonly IMReceiveMessageCallback OnReceivedBatchMessages = OnReceivedBatchMessagesCallback;
		[MonoPInvokeCallback(typeof(IMReceiveMessageCallback))]
		static void OnReceivedBatchMessagesCallback(string content,string jsonArcResult, IntPtr userData)
        {
            List<NIMReceivedMessage> msgs = null;
            if (!string.IsNullOrEmpty(content))
            {
                msgs = NimUtility.Json.JsonParser.Deserialize<List<NIMReceivedMessage>>(content);
            }
            NimUtility.DelegateConverter.InvokeOnce<ReceiveBatchMesaagesDelegate>(userData, msgs);
        }

        /// <summary>
        /// 撤回消息
        /// </summary>
        /// <param name="msgId">消息 id</param>
        /// <param name="notify">自定义通知</param>
        /// <param name="cb"></param>
        public static void RecallMessage(string msgId, string notify, RecallMessageDelegate cb)
        {
            var ptr = NimUtility.DelegateConverter.ConvertToIntPtr(cb);
            Dictionary<string, object> paramDic = new Dictionary<string, object>();
            paramDic[NIMIMMessage.ClientMessageId] = msgId;
            var json = NimUtility.Json.JsonParser.Serialize(paramDic);			
            TalkNativeMethods.nim_talk_recall_msg(json, notify, null, RecallMessageCb, ptr);
        }

        /// <summary>
        /// 撤回消息
        /// </summary>
        /// <param name="message">NIMIMMessage 对象</param>
        /// <param name="notify">自定义通知</param>
        /// <param name="cb"></param>
        public static void RecallMessage(NIMIMMessage message, string notify, RecallMessageDelegate cb)
        {
            var json = message.Serialize();
            var ptr = NimUtility.DelegateConverter.ConvertToIntPtr(cb);
            TalkNativeMethods.nim_talk_recall_msg(json, notify, null, RecallMessageCb, ptr);
        }

        private static readonly nim_talk_recall_msg_func RecallMessageCb = OnRecallMessageCompleted;

        [MonoPInvokeCallback(typeof(nim_talk_recall_msg_func))]
        private static void OnRecallMessageCompleted(int resCode, string content, string jsonExt, IntPtr userData)
        {
            var data = NimUtility.Json.JsonParser.Deserialize<RecallNotification[]>(content);
            NimUtility.DelegateConverter.InvokeOnce<RecallMessageDelegate>(userData, (ResponseCode)resCode, data);
        }

        /// <summary>
        /// 注册接收消息撤回通知的回调
        /// </summary>
        /// <param name="cb"></param>
        public static void RegRecallMessageCallback(RecallMessageDelegate cb)
        {
            var ptr = NimUtility.DelegateConverter.ConvertToIntPtr(cb);
            TalkNativeMethods.nim_talk_reg_recall_msg_cb(null, GlobalRecallMessageCb, ptr);
        }

        private static readonly nim_talk_recall_msg_func GlobalRecallMessageCb = RecallMessageCallbackFunc;

        [MonoPInvokeCallback(typeof(nim_talk_recall_msg_func))]
        private static void RecallMessageCallbackFunc(int resCode, string content, string jsonExt, IntPtr userData)
        {
            var data = NimUtility.Json.JsonParser.Deserialize<RecallNotification[]>(content);
            NimUtility.DelegateConverter.Invoke<RecallMessageDelegate>(userData, (ResponseCode)resCode, data);

        }
    }
}
