﻿using System.Collections.Concurrent;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Http;
using System.Threading;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Hosting.Server;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Builder;

namespace DanMu
{
    public class SocketHandler
    {
        private static List<WebSocket> _sockets = new List<WebSocket>();
        public const int BufferSize = 4096;
        public static object objLock = new object();
        public static List<DanmuData> historicalMessg = new List<DanmuData>();//存放历史消息

        /// <summary>
        /// 接收请求
        /// </summary>
        /// <param name="httpContext"></param>
        /// <param name="n"></param>
        /// <returns></returns>
        static async Task Acceptor(HttpContext httpContext, Func<Task> n)
        {
            
            if (!httpContext.WebSockets.IsWebSocketRequest)
                return;

            //建立一个WebSocket连接请求
            var socket = await httpContext.WebSockets.AcceptWebSocketAsync();
            //判断最大连接数
            if (Sockets.Count >= 100)
            {
                await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "连接超过最大限制 ...", CancellationToken.None);
                return;
            }

            lock (objLock)
            {
                Sockets.Add(socket);//加入
            }

            var buffer = new byte[BufferSize];

            //根据请求头获取 用户名
            string userName = httpContext.Request.Query["userName"].ToString();

            var danmu = new DanmuData();
            //群发上线通知
           // await SendToWebSocketsAsync(Sockets, chatData);

            while (true)
            {
                try
                {
                    //建立连接，阻塞等待接收消息
                    var incoming = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    //如果主动退出，则移除
                    if (incoming.MessageType == WebSocketMessageType.Close)//incoming.CloseStatus == WebSocketCloseStatus.EndpointUnavailable WebSocketCloseStatus.NormalClosure)
                    {
                        lock (objLock)
                        {
                            Sockets.Remove(socket);//移除   
                        }
                       // chatData = new ChatData() { info = userName + " 离开房间。还剩" + Sockets.Count + "人~~" };
                       // await SendToWebSocketsAsync(Sockets, chatData);
                        break; //【注意】：：这里一定要记得 跳出循环 （坑了好久）
                    }
                    //转字符串，然后序列化，然后赋值，然后再序列化
                    var chatDataStr = await ArraySegmentToStringAsync(new ArraySegment<byte>(buffer, 0, incoming.Count));
                    if (chatDataStr == "heartbeat")//如果是心跳检查，则直接跳过
                        continue;
                    danmu = JsonConvert.DeserializeObject<DanmuData>(chatDataStr);
                    
                    await SendToWebSocketsAsync(Sockets.Where(t => t != socket).ToList(), danmu);
                }
                catch (Exception ex) //因为 nginx 没有数据传输 会自动断开 然后就会异常。
                {
                    Log(ex.Message);
                    Sockets.Remove(socket);//移除
                   // chatData = new ChatData() { info = userName + " 离开房间。还剩" + Sockets.Count + "人~~" };
                   // await SendToWebSocketsAsync(Sockets, chatData);
                    //【注意】：：这里很重要 （如果不发送关闭会一直循环，且不能直接break。）
                    await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "未知异常 ...", CancellationToken.None);
                    // 后面 就不走了？ CloseAsync也不能 try 包起来？
                }
            }
        }

        /// <summary>
        /// 发送消息到所有人
        /// </summary>
        /// <param name="sockets"></param>
        /// <param name="arraySegment"></param>
        /// <returns></returns>
        public async static Task SendToWebSocketsAsync(List<WebSocket> sockets, DanmuData data)
        {
            SaveHistoricalMessg(data);//保存历史消息
            var chatData = JsonConvert.SerializeObject(data);
            var buffer = Encoding.UTF8.GetBytes(chatData);
            ArraySegment<byte> arraySegment = new ArraySegment<byte>(buffer);
            //循环发送消息
            for (int i = 0; i < sockets.Count; i++)
            {
                var tempsocket = sockets[i];
                if (tempsocket.State == WebSocketState.Open)
                {
                    //发送消息
                    await tempsocket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
        }

        static object lockSaveMsg = new object();

        public static List<WebSocket> Sockets { get => _sockets; set => _sockets = value; }

        /// <summary>
        /// 保存历史消息
        /// </summary>
        /// <param name="data"></param>
        public static void SaveHistoricalMessg(DanmuData data)
        {
            var size = 40;
            lock (lockSaveMsg)
            {
                historicalMessg.Add(data);
            }
            if (historicalMessg.Count >= size)
            {
                lock (lockSaveMsg)
                {
                    historicalMessg.RemoveRange(0, 30);
                }
            }
        }

        #region
        /// <summary>
        /// 转字符串
        /// </summary>
        /// <param name="arraySegment"></param>
        /// <returns></returns>
        static async Task<string> ArraySegmentToStringAsync(ArraySegment<byte> arraySegment)
        {
            using (var ms = new MemoryStream())
            {
                ms.Write(arraySegment.Array, arraySegment.Offset, arraySegment.Count);
                ms.Seek(0, SeekOrigin.Begin);
                using (var reader = new StreamReader(ms, Encoding.UTF8))
                {
                    return await reader.ReadToEndAsync();
                }
            }
        }
        #endregion

        /// <summary>
        /// 请求
        /// </summary>
        /// <param name="app"></param>
        public static void Map(IApplicationBuilder app)
        {
            app.UseWebSockets(); //nuget   导入 Microsoft.AspNetCore.WebSockets.Server
            app.Use(Acceptor);
        }

        /// <summary>
        /// 简单日志记录
        /// </summary>
        /// <param name="message"></param>
        private static void Log(string message)
        {
            dynamic type = (new Program()).GetType();
            string currentDirectory = Path.GetDirectoryName(type.Assembly.Location) + "/log.txt";
            File.WriteAllText(currentDirectory, message);
        }
    }
}
