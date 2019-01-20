using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
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
        public void SendFile(IPAddress ip, int port, string filePath, bool checksum = false)
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
                        // Header: 4 bytes for file length (int) + 4 bytes for file name length (int) + fileName + 20 bytes checksum
                        FileInfo fi = new FileInfo(filePath);
                        byte[] fileLen = BitConverter.GetBytes((int)fi.Length);
                        byte[] fileNameByte = Encoding.ASCII.GetBytes(Path.GetFileName(filePath));
                        byte[] fileNameLen = BitConverter.GetBytes(fileNameByte.Length);
                        byte[] checksumByte = new byte[20]; // SHA1 checksum is byte[20]
                        byte[] header = new byte[fileLen.Length + fileNameLen.Length + fileNameByte.Length + checksumByte.Length];
                        if (checksum)
                        {
                            using (var stream = new BufferedStream(File.OpenRead(filePath), 32768))
                            {
                                SHA1Managed sha = new SHA1Managed();
                                checksumByte = sha.ComputeHash(stream);
                                Debug.WriteLine(BitConverter.ToString(checksumByte).Replace("-", String.Empty).ToLower());
                            }
                        }
                        // Copy data into send byte array
                        int headerOffset = 0;
                        fileLen.CopyTo(header, headerOffset);
                        headerOffset += fileLen.Length;
                        fileNameLen.CopyTo(header, headerOffset);
                        headerOffset += fileNameLen.Length;
                        fileNameByte.CopyTo(header, headerOffset);
                        headerOffset += fileNameByte.Length;
                        checksumByte.CopyTo(header, headerOffset);

                        // Connect to end point
                        IPEndPoint ipEndPoint = new IPEndPoint(ip, port);
                        soc.SendBufferSize = BufferSize;
                        soc.Connect(ipEndPoint);
                        
                        // Update status and calculate progress percent increment
                        StatusMessage?.Invoke(this, $"Sending {Path.GetFileName(filePath)} to {ip}:{port} from {soc.LocalEndPoint}");
                        double progressBarIncrement = 100.0 / (Convert.ToDouble(fi.Length) / BufferSize);
                        int progressSingleIncrement = 0;

                        using (FileStream fsSource = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                        {
                            int numBytesToRead = (int)fsSource.Length;

                            // Initial packet
                            var firstPacket = (int)fi.Length >= BufferSize ? new byte[BufferSize] : new byte[header.Length + (int)fi.Length];
                            header.CopyTo(firstPacket,0);
                            // Fill rest of first packet up with file data
                            int transmitOffset = fsSource.Read(firstPacket, header.Length, firstPacket.Length - header.Length);
                            // Send initial packet
                            soc.Send(firstPacket, firstPacket.Length, SocketFlags.None);
                            _progress += progressBarIncrement;
                            // Transmit data in buffer size chunks
                            if (transmitOffset >= BufferSize - header.Length)
                            {
                                byte[] buffer = new byte[BufferSize];
                                while (true)
                                {
                                    // Calculate remaining bytes to send
                                    int sendsize = numBytesToRead - transmitOffset;
                                    // Send buffer sized chunk if enough data is left, otherwise send remaining bytes
                                    if (sendsize > BufferSize) sendsize = BufferSize;
                                    fsSource.Read(buffer, 0, sendsize);
                                    soc.Send(buffer, sendsize, SocketFlags.None);
                                    transmitOffset += sendsize;

                                    // Update progress only every percent
                                    _progress += progressBarIncrement;
                                    if (_progress > progressSingleIncrement + 1)
                                    {
                                        progressSingleIncrement = Convert.ToInt32(_progress);
                                        ProgressPercent?.Invoke(this, progressSingleIncrement);
                                    }

                                    if (transmitOffset >= numBytesToRead)
                                        break;
                                }
                            }
                        }

                        // Closing socket
                        soc.Shutdown(SocketShutdown.Both);
                        soc.Close();
                        StatusMessage?.Invoke(this, "File sent");
                        ProgressPercent?.Invoke(this, 100);
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
