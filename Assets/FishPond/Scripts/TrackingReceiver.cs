using System;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FishPond
{
    /// <summary>
    /// 카메라 추적 프로그램(tools/tracker/ir_tracker.py)이 보내는 뜰채 좌표를 UDP로 받습니다.
    ///
    /// 메시지 형식 (한 줄에 뜰채 하나, 여러 줄 가능):
    ///   "1 0.523 0.331"   → P1 뜰채가 투사 화면 가로 52.3%, 위에서 33.1% 지점
    ///   "1 -1 -1"         → P1 뜰채를 잃어버림
    /// 좌표는 투사 화면 기준 0~1, 원점은 왼쪽 위입니다.
    /// </summary>
    public class TrackingReceiver : IDisposable
    {
        readonly object gate = new object();
        readonly Vector2[] positions = new Vector2[4];
        readonly double[] stamps = new double[4];
        readonly Stopwatch clock = Stopwatch.StartNew();

        UdpClient udp;
        Thread thread;
        volatile bool running;

        public int Port { get; private set; }
        public int Packets { get; private set; }
        public string LastMessage { get; private set; }
        public string Error { get; private set; }

        public bool Start(int port)
        {
            Stop();
            try
            {
                udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));
                Port = port;
                running = true;
                thread = new Thread(Loop) { IsBackground = true, Name = "FishPond Tracking" };
                thread.Start();
                Error = null;
                return true;
            }
            catch (Exception e)
            {
                Error = e.Message;
                Debug.LogWarning("[FishPond] 추적 포트 " + port + "를 열지 못했습니다: " + e.Message);
                return false;
            }
        }

        void Loop()
        {
            var any = new IPEndPoint(IPAddress.Any, 0);
            while (running)
            {
                try
                {
                    var data = udp.Receive(ref any);
                    Parse(Encoding.UTF8.GetString(data));
                }
                catch (SocketException) { if (!running) break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception e) { Error = e.Message; }
            }
        }

        void Parse(string msg)
        {
            double now = clock.Elapsed.TotalSeconds;
            var lines = msg.Split(new[] { '\n', ';' }, StringSplitOptions.RemoveEmptyEntries);
            lock (gate)
            {
                Packets++;
                LastMessage = msg.Trim();
                foreach (var raw in lines)
                {
                    var p = raw.Trim().Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length < 3) continue;
                    var idText = p[0].TrimStart('P', 'p');
                    int id;
                    float x, y;
                    if (!int.TryParse(idText, NumberStyles.Integer, CultureInfo.InvariantCulture, out id)) continue;
                    if (!float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out x)) continue;
                    if (!float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out y)) continue;
                    int i = id - 1;
                    if (i < 0 || i > 3) continue;
                    if (x < 0f || y < 0f) { stamps[i] = 0; continue; }
                    positions[i] = new Vector2(x, y);
                    stamps[i] = now;
                }
            }
        }

        /// <summary>maxAge초 안에 받은 좌표가 있으면 true</summary>
        public bool TryGet(int index, double maxAge, out Vector2 viewport)
        {
            lock (gate)
            {
                viewport = positions[index];
                return stamps[index] > 0 && clock.Elapsed.TotalSeconds - stamps[index] <= maxAge;
            }
        }

        public void Stop()
        {
            running = false;
            if (udp != null) { try { udp.Close(); } catch (Exception) { } udp = null; }
            if (thread != null) { thread.Join(200); thread = null; }
        }

        public void Dispose() { Stop(); }
    }
}
