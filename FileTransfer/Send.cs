using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace FileTransfer
{
    public class Send
    {

        #region Fields and properties

        /// <summary>
        /// https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.tcpclient.sendbuffersize?view=netframework-4.7.2
        /// </summary>
        private int _bufferSize = 8192;
        public int BufferSize
        {
            get => _bufferSize;
            set
            {
                lock (_lockSending)
                {
                    _bufferSize = value;
                }
            }
        }

        /// <summary>
        /// For progressbar in UI
        /// </summary>
        private double _progress;

        /// <summary>
        /// Sending lock
        /// </summary>
        private readonly object _lockSending = new object();


        #endregion

        #region Events

        public delegate void StatusMessageEventHandler(object sender, string status);
        public event StatusMessageEventHandler StatusMessage;

        public delegate void ProgressPercentEventHandler(object sender, double progress);
        public event ProgressPercentEventHandler ProgressPercent;

        public delegate void CleanupEventHandler(object sender, EventArgs args);
        public event CleanupEventHandler Cleanup;

        #endregion

        /// <summary>
        /// Sends a file with header composed of file size and file name
        /// </summary>
        /// <param name="ip">Receiving end ip address</param>
        /// <param name="port">Receiving end port</param>
        /// <param name="filePath">Full path to file</param>
        public void SendFile(IPAddress ip, int port, string filePath)
        {
            // lock to prevent bad things in case buffer size is changed
            lock (_lockSending)
            {
                try
                {
                    // Reset progress and update status
                    _progress = 0;
                    ProgressPercent?.Invoke(this, _progress);
                    StatusMessage?.Invoke(this, "Connecting...");

                    // Open socket
                    using (Socket soc = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                    {
                        // Get file info
                        // Header: 4 byte for file length (int) + 4 byte for file name length (int) + fileName
                        byte[] fileData = File.ReadAllBytes(filePath);
                        byte[] fileLen = BitConverter.GetBytes(fileData.Length);
                        byte[] fileNameByte = Encoding.ASCII.GetBytes(Path.GetFileName(filePath));
                        byte[] fileNameLen = BitConverter.GetBytes(fileNameByte.Length);
                        byte[] sendData = new byte[4 + 4 + fileNameByte.Length + fileData.Length];
                        // Copy data into send byte array
                        fileLen.CopyTo(sendData, 0);
                        fileNameLen.CopyTo(sendData, 4);
                        fileNameByte.CopyTo(sendData, 8);
                        fileData.CopyTo(sendData, 4 + 4 + fileNameByte.Length);

                        // Connect to end point
                        IPEndPoint ipEndPoint = new IPEndPoint(ip, port);
                        soc.SendBufferSize = BufferSize;
                        soc.Connect(ipEndPoint);
                        
                        // Update status and calculate progress percent increment
                        StatusMessage?.Invoke(this, $"Sending {Path.GetFileName(filePath)} to {ip}:{port} from {soc.LocalEndPoint}");
                        double progressBarIncrement = 100 / (fileData.Length / (double)BufferSize);
                        int progressSingleIncrement = 0;

                        // Transmit data in buffer size chunks
                        int transmitOffset = 0;
                        while (true)
                        {
                            // Calculate remaining bytes to send
                            int sendsize = sendData.Length - transmitOffset;
                            // Send buffer sized chunk if enough data is left, otherwise send remaining bytes
                            if (sendsize > BufferSize) sendsize = BufferSize;
                            int sendret = soc.Send(sendData, transmitOffset, sendsize, SocketFlags.None);
                            transmitOffset += sendret;

                            // Update progress only every percent
                            _progress += progressBarIncrement;
                            if (_progress > progressSingleIncrement)
                            {
                                progressSingleIncrement = Convert.ToInt32(_progress);
                                ProgressPercent?.Invoke(this, progressSingleIncrement);
                            }

                            if (transmitOffset >= sendData.Length)
                                break;
                        }

                        // Closing socket
                        soc.Shutdown(SocketShutdown.Both);
                        soc.Close();
                        StatusMessage?.Invoke(this, "File sent");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                    StatusMessage?.Invoke(this, ex.Message);
                }
                finally
                {
                    // Send cleanup event, e.g. usefull to enable buttens again
                    Cleanup?.Invoke(this, EventArgs.Empty);
                }
            }
        }
    }
}
