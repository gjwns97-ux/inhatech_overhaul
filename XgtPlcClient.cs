using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace 인하테크개조
{
    /// <summary>
    /// LS ELECTRIC XGT Dedicated Ethernet client.
    ///
    /// Target configuration:
    /// - CPU: XBC-DR32H (XGB)
    /// - Ethernet: XBL-EMTA, slot 3
    /// - Server mode: XGT Server
    /// - TCP port: 2004
    ///
    /// Form1 usage:
    ///     plc.Connect("192.168.1.2");
    ///     bool bit = plc.ReadBit("M100");
    ///     ushort word = plc.ReadWord("D3260");
    ///     int dword = plc.ReadDWord("D3600");
    /// </summary>
    public sealed class XgtPlcClient : IDisposable
    {
        private const ushort CommandReadRequest = 0x0054;
        private const ushort CommandReadResponse = 0x0055;
        private const ushort CommandWriteRequest = 0x0058;
        private const ushort CommandWriteResponse = 0x0059;

        private const ushort DataTypeBit = 0x0000;
        private const ushort DataTypeByte = 0x0001;
        private const ushort DataTypeWord = 0x0002;
        private const ushort DataTypeDWord = 0x0003;
        private const ushort DataTypeContinuous = 0x0014;

        private const int HeaderLength = 20;
        private const int MaxApplicationLength = 65535;

        private readonly object communicationLock = new object();

        private TcpClient client;
        private NetworkStream stream;
        private ushort invokeId = 1;

        /// <summary>
        /// XGB CPU information code. XBC/XBM family default: 0xB0.
        /// </summary>
        public byte CpuInfo { get; set; } = 0xB0;

        /// <summary>
        /// FEnet module position. XBL-EMTA is installed in slot 3, so default is 0x03.
        /// If the PLC rejects the header, test 0x00 as some firmware accepts/uses zero in a client request.
        /// </summary>
        public byte FEnetPosition { get; set; } = 0x03;

        public bool IsConnected
        {
            get
            {
                return client != null &&
                       client.Connected &&
                       stream != null;
            }
        }

        public void Connect(
            string ipAddress,
            int port = 2004,
            int timeoutMs = 2000)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                throw new ArgumentException("PLC IP 주소가 비어 있습니다.", nameof(ipAddress));

            if (port <= 0 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port));

            if (timeoutMs <= 0)
                throw new ArgumentOutOfRangeException(nameof(timeoutMs));

            Disconnect();

            client = new TcpClient
            {
                ReceiveTimeout = timeoutMs,
                SendTimeout = timeoutMs,
                NoDelay = true
            };

            IAsyncResult asyncResult = null;

            try
            {
                asyncResult = client.BeginConnect(ipAddress, port, null, null);

                if (!asyncResult.AsyncWaitHandle.WaitOne(timeoutMs))
                    throw new TimeoutException(
                        $"PLC TCP 연결 시간 초과: {ipAddress}:{port}");

                client.EndConnect(asyncResult);

                stream = client.GetStream();
                stream.ReadTimeout = timeoutMs;
                stream.WriteTimeout = timeoutMs;
            }
            catch
            {
                Disconnect();
                throw;
            }
            finally
            {
                asyncResult?.AsyncWaitHandle.Close();
            }
        }

        // ============================================================
        // Public read methods
        // ============================================================

        public bool ReadBit(string address)
        {
            string variable = ToIndividualVariable(address, DataTypeBit);
            byte[] data = ReadIndividual(variable, DataTypeBit);

            if (data.Length < 1)
                throw new IOException("PLC Bit 응답 데이터가 부족합니다.");

            return data[0] != 0;
        }

        public byte ReadByte(string address)
        {
            string variable = ToIndividualVariable(address, DataTypeByte);
            byte[] data = ReadIndividual(variable, DataTypeByte);

            if (data.Length < 1)
                throw new IOException("PLC Byte 응답 데이터가 부족합니다.");

            return data[0];
        }

        public ushort ReadWord(string address)
        {
            string variable = ToIndividualVariable(address, DataTypeWord);
            byte[] data = ReadIndividual(variable, DataTypeWord);

            if (data.Length < 2)
                throw new IOException("PLC Word 응답 데이터가 부족합니다.");

            return ReadUInt16(data, 0);
        }

        public short ReadInt16(string address)
        {
            return unchecked((short)ReadWord(address));
        }

        /// <summary>
        /// Reads two consecutive D registers in one XGT continuous-read request.
        /// Example: D3600 -> byte address %DB7200, length 4.
        /// </summary>
        public int ReadDWord(string address)
        {
            int wordAddress = ParseWordDeviceAddress(address, 'D');
            byte[] data = ReadContinuousBytes(
                $"%DB{checked(wordAddress * 2)}",
                4);

            if (data.Length < 4)
                throw new IOException("PLC DWord 응답 데이터가 부족합니다.");

            uint value =
                (uint)data[0] |
                ((uint)data[1] << 8) |
                ((uint)data[2] << 16) |
                ((uint)data[3] << 24);

            return unchecked((int)value);
        }

        public uint ReadUInt32(string address)
        {
            return unchecked((uint)ReadDWord(address));
        }

        /// <summary>
        /// Reads consecutive D registers.
        /// Example: ReadWords("D100", 10) reads D100 through D109.
        /// </summary>
        public ushort[] ReadWords(string startAddress, int wordCount)
        {
            if (wordCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(wordCount));

            int wordAddress = ParseWordDeviceAddress(startAddress, 'D');
            int byteCount = checked(wordCount * 2);

            byte[] data = ReadContinuousBytes(
                $"%DB{checked(wordAddress * 2)}",
                byteCount);

            if (data.Length != byteCount)
                throw new IOException(
                    $"PLC 연속 읽기 길이 불일치: 요청 {byteCount}바이트, 응답 {data.Length}바이트");

            ushort[] result = new ushort[wordCount];

            for (int i = 0; i < wordCount; i++)
                result[i] = ReadUInt16(data, i * 2);

            return result;
        }

        // ============================================================
        // Public write methods
        // ============================================================

        public void WriteBit(string address, bool value)
        {
            string variable = ToIndividualVariable(address, DataTypeBit);
            WriteIndividual(
                variable,
                DataTypeBit,
                new[] { value ? (byte)1 : (byte)0 });
        }

        public void WriteByte(string address, byte value)
        {
            string variable = ToIndividualVariable(address, DataTypeByte);
            WriteIndividual(variable, DataTypeByte, new[] { value });
        }

        public void WriteWord(string address, int value)
        {
            WriteWord(address, unchecked((ushort)value));
        }

        public void WriteWord(string address, ushort value)
        {
            string variable = ToIndividualVariable(address, DataTypeWord);

            WriteIndividual(
                variable,
                DataTypeWord,
                new[]
                {
                    (byte)(value & 0xFF),
                    (byte)(value >> 8)
                });
        }

        /// <summary>
        /// Writes a signed 32-bit value into two consecutive D registers in one request.
        /// </summary>
        public void WriteDWord(string address, int value)
        {
            int wordAddress = ParseWordDeviceAddress(address, 'D');
            uint raw = unchecked((uint)value);

            byte[] data =
            {
                (byte)(raw & 0xFF),
                (byte)((raw >> 8) & 0xFF),
                (byte)((raw >> 16) & 0xFF),
                (byte)((raw >> 24) & 0xFF)
            };

            WriteContinuousBytes(
                $"%DB{checked(wordAddress * 2)}",
                data);
        }

        public void WriteWords(string startAddress, ushort[] values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            if (values.Length == 0)
                throw new ArgumentException("쓸 데이터가 없습니다.", nameof(values));

            int wordAddress = ParseWordDeviceAddress(startAddress, 'D');
            byte[] data = new byte[checked(values.Length * 2)];

            for (int i = 0; i < values.Length; i++)
            {
                data[i * 2] = (byte)(values[i] & 0xFF);
                data[i * 2 + 1] = (byte)(values[i] >> 8);
            }

            WriteContinuousBytes(
                $"%DB{checked(wordAddress * 2)}",
                data);
        }

        // ============================================================
        // XGT request methods
        // ============================================================

        private byte[] ReadIndividual(string variableName, ushort dataType)
        {
            byte[] variableBytes = Encoding.ASCII.GetBytes(variableName);
            var application = new List<byte>();

            AddUInt16(application, CommandReadRequest);
            AddUInt16(application, dataType);
            AddUInt16(application, 0x0000); // reserved
            AddUInt16(application, 0x0001); // block count
            AddUInt16(application, checked((ushort)variableBytes.Length));
            application.AddRange(variableBytes);

            return SendReadRequest(application.ToArray());
        }

        private byte[] ReadContinuousBytes(string byteVariableName, int byteCount)
        {
            if (byteCount <= 0 || byteCount > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(byteCount));

            byte[] variableBytes =
                Encoding.ASCII.GetBytes(byteVariableName);

            var application = new List<byte>();

            AddUInt16(application, CommandReadRequest);
            AddUInt16(application, DataTypeContinuous);
            AddUInt16(application, 0x0000); // reserved
            AddUInt16(application, 0x0001); // block count
            AddUInt16(application, checked((ushort)variableBytes.Length));
            application.AddRange(variableBytes);
            AddUInt16(application, checked((ushort)byteCount));

            return SendReadRequest(application.ToArray());
        }

        private void WriteIndividual(
            string variableName,
            ushort dataType,
            byte[] data)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("쓰기 데이터가 없습니다.", nameof(data));

            byte[] variableBytes = Encoding.ASCII.GetBytes(variableName);
            var application = new List<byte>();

            AddUInt16(application, CommandWriteRequest);
            AddUInt16(application, dataType);
            AddUInt16(application, 0x0000); // reserved
            AddUInt16(application, 0x0001); // block count
            AddUInt16(application, checked((ushort)variableBytes.Length));
            application.AddRange(variableBytes);
            AddUInt16(application, checked((ushort)data.Length));
            application.AddRange(data);

            SendWriteRequest(application.ToArray());
        }

        private void WriteContinuousBytes(
            string byteVariableName,
            byte[] data)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("쓰기 데이터가 없습니다.", nameof(data));

            if (data.Length > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(data));

            byte[] variableBytes =
                Encoding.ASCII.GetBytes(byteVariableName);

            var application = new List<byte>();

            AddUInt16(application, CommandWriteRequest);
            AddUInt16(application, DataTypeContinuous);
            AddUInt16(application, 0x0000); // reserved
            AddUInt16(application, 0x0001); // block count
            AddUInt16(application, checked((ushort)variableBytes.Length));
            application.AddRange(variableBytes);
            AddUInt16(application, checked((ushort)data.Length));
            application.AddRange(data);

            SendWriteRequest(application.ToArray());
        }

        private byte[] SendReadRequest(byte[] application)
        {
            byte[] response = SendAndReceive(application);
            return ParseReadResponse(response);
        }

        private void SendWriteRequest(byte[] application)
        {
            byte[] response = SendAndReceive(application);
            ParseWriteResponse(response);
        }

        private byte[] SendAndReceive(byte[] application)
        {
            EnsureConnected();

            lock (communicationLock)
            {
                ushort currentInvokeId = NextInvokeId();

                byte[] header =
                    CreateXgtHeader(currentInvokeId, application.Length);

                byte[] request =
                    new byte[header.Length + application.Length];

                Buffer.BlockCopy(
                    header, 0,
                    request, 0,
                    header.Length);

                Buffer.BlockCopy(
                    application, 0,
                    request, header.Length,
                    application.Length);

                try
                {
                    stream.Write(request, 0, request.Length);
                    stream.Flush();

                    byte[] responseHeader = ReadExact(HeaderLength);
                    ValidateHeader(responseHeader, currentInvokeId);

                    int applicationLength =
                        ReadUInt16(responseHeader, 16);

                    if (applicationLength <= 0 ||
                        applicationLength > MaxApplicationLength)
                    {
                        throw new IOException(
                            $"PLC 응답 Application 길이가 잘못되었습니다: {applicationLength}");
                    }

                    return ReadExact(applicationLength);
                }
                catch
                {
                    // TCP 연결은 Connected 속성만으로 단절을 정확히 알 수 없으므로,
                    // 통신 예외가 발생하면 현재 연결을 폐기한다.
                    Disconnect();
                    throw;
                }
            }
        }

        // ============================================================
        // XGT Ethernet header
        // ============================================================

        private byte[] CreateXgtHeader(
            ushort requestInvokeId,
            int applicationLength)
        {
            if (applicationLength <= 0 ||
                applicationLength > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(applicationLength));
            }

            byte[] header = new byte[HeaderLength];

            byte[] company =
                Encoding.ASCII.GetBytes("LSIS-XGT");

            Buffer.BlockCopy(company, 0, header, 0, company.Length);

            header[8] = 0x00;
            header[9] = 0x00;

            // PLC information: request frame
            header[10] = 0x00;
            header[11] = 0x00;

            // XGB/XBC CPU information
            header[12] = CpuInfo;

            // Client(PC) -> Server(PLC)
            header[13] = 0x33;

            WriteUInt16(header, 14, requestInvokeId);
            WriteUInt16(header, 16, checked((ushort)applicationLength));

            // XBL-EMTA slot 3
            header[18] = FEnetPosition;

            // BCC: lower byte of sum from byte 0 through byte 18
            int sum = 0;
            for (int i = 0; i < 19; i++)
                sum += header[i];

            header[19] = (byte)(sum & 0xFF);

            return header;
        }

        private void ValidateHeader(
            byte[] header,
            ushort requestInvokeId)
        {
            if (header == null || header.Length != HeaderLength)
                throw new IOException("PLC 응답 헤더 길이가 잘못되었습니다.");

            string company =
                Encoding.ASCII.GetString(header, 0, 8);

            if (!string.Equals(
                    company,
                    "LSIS-XGT",
                    StringComparison.Ordinal))
            {
                throw new IOException(
                    $"XGT 응답이 아닙니다. Company ID={company}");
            }

            ushort responseInvokeId =
                ReadUInt16(header, 14);

            if (responseInvokeId != requestInvokeId)
            {
                throw new IOException(
                    $"Invoke ID 불일치: 요청={requestInvokeId}, 응답={responseInvokeId}");
            }

            byte expectedBcc = CalculateBcc(header);

            if (header[19] != expectedBcc)
            {
                throw new IOException(
                    $"XGT Header BCC 오류: 수신=0x{header[19]:X2}, 계산=0x{expectedBcc:X2}");
            }
        }

        private static byte CalculateBcc(byte[] header)
        {
            int sum = 0;

            for (int i = 0; i < 19; i++)
                sum += header[i];

            return (byte)(sum & 0xFF);
        }

        // ============================================================
        // Response parsing
        // ============================================================

        private byte[] ParseReadResponse(byte[] response)
        {
            EnsureMinimumLength(response, 10);

            ushort command = ReadUInt16(response, 0);

            if (command != CommandReadResponse)
            {
                throw new IOException(
                    $"읽기 응답 명령이 아닙니다: 0x{command:X4}");
            }

            ThrowIfPlcError(response);

            ushort blockCount = ReadUInt16(response, 8);

            if (blockCount != 1)
            {
                throw new IOException(
                    $"지원하지 않는 응답 블록 수입니다: {blockCount}");
            }

            EnsureMinimumLength(response, 12);

            ushort dataLength = ReadUInt16(response, 10);
            int requiredLength = checked(12 + dataLength);

            if (response.Length < requiredLength)
            {
                throw new IOException(
                    $"읽기 응답 길이 불일치: 필요={requiredLength}, 실제={response.Length}");
            }

            byte[] result = new byte[dataLength];

            Buffer.BlockCopy(
                response, 12,
                result, 0,
                dataLength);

            return result;
        }

        private void ParseWriteResponse(byte[] response)
        {
            EnsureMinimumLength(response, 10);

            ushort command = ReadUInt16(response, 0);

            if (command != CommandWriteResponse)
            {
                throw new IOException(
                    $"쓰기 응답 명령이 아닙니다: 0x{command:X4}");
            }

            ThrowIfPlcError(response);

            ushort blockCount = ReadUInt16(response, 8);

            if (blockCount != 1)
            {
                throw new IOException(
                    $"쓰기 응답 블록 수가 올바르지 않습니다: {blockCount}");
            }
        }

        private static void ThrowIfPlcError(byte[] response)
        {
            ushort errorStatus = ReadUInt16(response, 6);

            if (errorStatus == 0)
                return;

            ushort errorDetail =
                response.Length >= 12
                    ? ReadUInt16(response, 10)
                    : (ushort)0;

            throw new IOException(
                $"PLC XGT 오류: 상태=0x{errorStatus:X4}, 상세=0x{errorDetail:X4}");
        }

        // ============================================================
        // Address conversion
        // ============================================================

        private static string ToIndividualVariable(
            string address,
            ushort dataType)
        {
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException(
                    "PLC 주소가 비어 있습니다.",
                    nameof(address));

            string normalized =
                address.Trim().ToUpperInvariant();

            if (normalized.StartsWith("%", StringComparison.Ordinal))
                return normalized;

            if (normalized.Length < 2)
                throw new ArgumentException(
                    $"PLC 주소 형식이 잘못되었습니다: {address}");

            char device = normalized[0];
            string number = normalized.Substring(1);

            ValidateDecimalAddress(number, address);

            switch (dataType)
            {
                case DataTypeBit:
                    return $"%{device}X{number}";

                case DataTypeByte:
                    return $"%{device}B{number}";

                case DataTypeWord:
                    return $"%{device}W{number}";

                case DataTypeDWord:
                    return $"%{device}D{number}";

                default:
                    throw new NotSupportedException(
                        $"지원하지 않는 XGT 데이터 형식: 0x{dataType:X4}");
            }
        }

        private static int ParseWordDeviceAddress(
            string address,
            char requiredDevice)
        {
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException(
                    "PLC 주소가 비어 있습니다.",
                    nameof(address));

            string normalized =
                address.Trim().ToUpperInvariant();

            if (normalized.StartsWith("%DW", StringComparison.Ordinal))
                normalized = "D" + normalized.Substring(3);

            if (normalized.Length < 2 ||
                normalized[0] != requiredDevice)
            {
                throw new ArgumentException(
                    $"현재 연속 읽기/쓰기는 {requiredDevice} 영역만 지원합니다: {address}");
            }

            string number = normalized.Substring(1);
            ValidateDecimalAddress(number, address);

            return int.Parse(number, CultureInfo.InvariantCulture);
        }

        private static void ValidateDecimalAddress(
            string number,
            string originalAddress)
        {
            int parsed;

            if (!int.TryParse(
                    number,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out parsed) ||
                parsed < 0)
            {
                throw new ArgumentException(
                    $"PLC 주소 숫자가 올바르지 않습니다: {originalAddress}");
            }
        }

        // ============================================================
        // Utilities
        // ============================================================

        private ushort NextInvokeId()
        {
            ushort current = invokeId;

            invokeId++;

            if (invokeId == 0)
                invokeId = 1;

            return current;
        }

        private void EnsureConnected()
        {
            if (!IsConnected)
                throw new InvalidOperationException(
                    "PLC가 연결되어 있지 않습니다.");
        }

        private byte[] ReadExact(int count)
        {
            byte[] buffer = new byte[count];
            int received = 0;

            while (received < count)
            {
                int read = stream.Read(
                    buffer,
                    received,
                    count - received);

                if (read <= 0)
                    throw new IOException(
                        "PLC TCP 연결이 종료되었습니다.");

                received += read;
            }

            return buffer;
        }

        private static void EnsureMinimumLength(
            byte[] buffer,
            int minimumLength)
        {
            if (buffer == null || buffer.Length < minimumLength)
            {
                throw new IOException(
                    $"PLC 응답 프레임이 너무 짧습니다. 최소={minimumLength}, 실제={buffer?.Length ?? 0}");
            }
        }

        private static ushort ReadUInt16(
            byte[] buffer,
            int offset)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0 || offset + 1 >= buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            return (ushort)(
                buffer[offset] |
                (buffer[offset + 1] << 8));
        }

        private static void WriteUInt16(
            byte[] buffer,
            int offset,
            ushort value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)(value >> 8);
        }

        private static void AddUInt16(
            List<byte> buffer,
            ushort value)
        {
            buffer.Add((byte)(value & 0xFF));
            buffer.Add((byte)(value >> 8));
        }

        public void Disconnect()
        {
            try
            {
                stream?.Close();
                stream?.Dispose();
            }
            catch
            {
                // Disconnect must not hide the original communication error.
            }

            try
            {
                client?.Close();
                client?.Dispose();
            }
            catch
            {
                // Ignore cleanup errors.
            }

            stream = null;
            client = null;
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}