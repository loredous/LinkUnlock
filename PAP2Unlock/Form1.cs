using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.IO;

namespace PAP2Unlock
{
    public partial class Form1 : Form
    {
        UdpClient DHCPServer;
        UdpClient DNSServer;
        UdpClient TFTPServer69;
        UdpClient TFTPServer21;
        UdpClient TFTPServer2400;

        public Form1()
        {
           
            InitializeComponent();
        }

        public void AppendTextBox(string value)
        {
            if (InvokeRequired)
            {
                this.Invoke(new Action<string>(AppendTextBox), new object[] { value });
                return;
            }
            txtDebug.AppendText(value + Environment.NewLine);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            FillUI();
        }

        private void FillUI()
        {
            foreach(var NetInterface in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                foreach (var Address in NetInterface.GetIPProperties().UnicastAddresses)
                {
                    if (Address.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        cmbIP.Items.Add(Address.Address);
                    }
                }
            }
            cmbIP.SelectedIndex = 0;
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            foreach (Control Cont in grpConfig.Controls)
            {
                Cont.Enabled = false;
            }
            btnStop.Enabled = true;

            

            DoDHCP();
            DoDNS();
            DoTFTP();
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            if (DHCPServer != null)
            {
                lock (DHCPServer)
                {
                    DHCPServer.Close();
                }
            }
            if (DNSServer != null)
            {
                lock (DNSServer)
                {
                    DNSServer.Close();
                }
            }
            if (TFTPServer69 != null)
            {
                lock (TFTPServer69)
                {
                    TFTPServer69.Close();
                }
            }
            if (TFTPServer2400 != null)
            {
                lock (TFTPServer2400)
                {
                    TFTPServer2400.Close();
                }
            }
            if (TFTPServer21 != null)
            {
                lock (TFTPServer21)
                {
                    TFTPServer21.Close();
                }
            }
            foreach (Control Cont in grpConfig.Controls)
            {
                Cont.Enabled = true;
            }
            btnStop.Enabled = false;
        }

        private string BytesToString(byte[] value)
        {
            StringBuilder builder = new StringBuilder();
            foreach (var OneByte in value)
            {
                builder.Append(OneByte.ToString("X2"));
                builder.Append("|");
            }
            return builder.ToString();
        }

        private class UdpState
        {
            public IPEndPoint EndPoint;
            public UdpClient UDPClient;
            public IPAddress BindAddr;
        }

        #region TFTP

        private void DoTFTP()
        {
            TFTPServer69 = new UdpClient(new IPEndPoint((IPAddress)cmbIP.SelectedItem, 69));
            TFTPServer21 = new UdpClient(new IPEndPoint((IPAddress)cmbIP.SelectedItem, 21));
            TFTPServer2400 =  new UdpClient(new IPEndPoint((IPAddress)cmbIP.SelectedItem, 2400));
            IPEndPoint Client = new IPEndPoint(IPAddress.Any, 0);
            UdpState State69 = new UdpState();
            State69.UDPClient = TFTPServer69;
            State69.EndPoint = Client;
            State69.BindAddr = (IPAddress)cmbIP.SelectedItem;
            TFTPServer69.BeginReceive(new AsyncCallback(TFTPHandleAsync), State69);

            UdpState State21 = new UdpState();
            State21.UDPClient = TFTPServer21;
            State21.EndPoint = Client;
            State21.BindAddr = (IPAddress)cmbIP.SelectedItem;
            TFTPServer21.BeginReceive(new AsyncCallback(TFTPHandleAsync), State21);

            UdpState State2400 = new UdpState();
            State2400.UDPClient = TFTPServer2400;
            State2400.EndPoint = Client;
            State2400.BindAddr = (IPAddress)cmbIP.SelectedItem;
            TFTPServer2400.BeginReceive(new AsyncCallback(TFTPHandleAsync), State2400);
        }

        private void TFTPHandleAsync(IAsyncResult ar)
        {
            TFTPMessage Message = new TFTPMessage();
            UdpState State = (UdpState)ar.AsyncState;
            lock (State.UDPClient)
            {
                if (State.UDPClient.Client != null)
                {
                    byte[] buffer = State.UDPClient.EndReceive(ar, ref State.EndPoint);
                    Message = TFTPMessage.Parse(buffer);


                    State.UDPClient.BeginReceive(new AsyncCallback(DHCPHandleAsync), State);
                }
            }
        }

        private class TFTPMessage
        {
            public PacketType Type = PacketType.ERROR;
            public string Filename = "";
            public int BlockNum = 0;
            public Mode TransferMode = Mode.octet;
            
            public enum Mode
            {
                octet,
                netascii,
                mail
            }

            public enum PacketType
            {
                RRQ = 1,
                WRQ = 2,
                DATA = 3,
                ACK = 4,
                ERROR = 5
            }

            public static TFTPMessage Parse(byte[] Data)
            {
                TFTPMessage Result = new TFTPMessage();
                Data.ConvertEndian(0, 2);
                Result.Type = (PacketType)BitConverter.ToInt16(Data, 0);
                switch (Result.Type)
                {
                    case PacketType.RRQ:
                    case PacketType.WRQ:
                        Data = Data.SubArray(3);
                        Result.Filename = GetZeroTerminatedString(Data);
                        Data = Data.SubArray(Array.IndexOf<byte>(Data, 0x00) + 1);
                        Result.TransferMode = (Mode)Enum.Parse(typeof(Mode), GetZeroTerminatedString(Data));
                        break;
                    case PacketType.DATA:
                        break;
                    case PacketType.ACK:
                        break;
                    case PacketType.ERROR:
                        break;
                    default:
                        break;
                }
                return Result;
            }

            public static string GetZeroTerminatedString(byte[] Data)
            {
                return System.Text.Encoding.Default.GetString(Data, 0, Array.IndexOf<byte>(Data, 0x00));
            }
        }

        #endregion TFTP

        #region DHCP

        private void DoDHCP()
        {
            DHCPServer = new UdpClient(new IPEndPoint((IPAddress)cmbIP.SelectedItem, 67));
            IPEndPoint Client = new IPEndPoint(IPAddress.Any, 0);
            UdpState State = new UdpState();
            State.UDPClient = DHCPServer;
            State.EndPoint = Client;
            State.BindAddr = (IPAddress)cmbIP.SelectedItem;
            DHCPServer.BeginReceive(new AsyncCallback(DHCPHandleAsync), State);
        }
        
        private void DHCPHandleAsync(IAsyncResult ar)
        {

            //TODO: Modify to base address off of bind addr.
            DHCPMessage Message = new DHCPMessage();
            UdpState State = (UdpState)ar.AsyncState;
            lock (State.UDPClient)
            {
                if (State.UDPClient.Client != null)
                {
                byte[] buffer = State.UDPClient.EndReceive(ar, ref State.EndPoint);
                Message = DHCPMessage.Parse(buffer);

                if (Message.Options.OptionValues.Find(x => x.OptionID == 53).Data[0] == 0x01)
                {
                    //DHCP Discover!
                    AppendTextBox(string.Format("Got DHCP Discover from {0}", Message.MAC.ToString()));

                    Message.Operation = DHCPMessage.OperationType.REPLY;
                    Message.Hops = 0;
                    Message.YourIP = new IPAddress(new byte[4] { 0x0a, 0x00, 0x00, 0x02 });
                    Message.ServerIP = new IPAddress(new byte[4] { 0x0a, 0x00, 0x00, 0x01 });
                    Message.GatewayIP = new IPAddress(new byte[4] { 0x0a, 0x00, 0x00, 0x01 });
                    Message.Options = new DHCPOptions();

                    Message.Options.AddOptionValue(DHCPOptions.DHCPOptionValue.DHCPOptionIDs.SubnetMask, new byte[] { 0xff, 0x00, 0x00, 0x00 });
                    Message.Options.AddOptionValue(DHCPOptions.DHCPOptionValue.DHCPOptionIDs.Gateway, new byte[] { 0x0a, 0x00, 0x00, 0x01 });
                    Message.Options.AddOptionValue(DHCPOptions.DHCPOptionValue.DHCPOptionIDs.DNSServers, new byte[] { 0x0a, 0x00, 0x00, 0x01 });
                    Message.Options.AddOptionValue(DHCPOptions.DHCPOptionValue.DHCPOptionIDs.LeaseTime, new byte[] { 0xFF, 0xff, 0xff, 0xff });
                    Message.Options.AddOptionValue(DHCPOptions.DHCPOptionValue.DHCPOptionIDs.DHCPOffer, new byte[] { 0x02 });
                    Message.Options.AddOptionValue(DHCPOptions.DHCPOptionValue.DHCPOptionIDs.ServerIP, new byte[] { 0x0a, 0x00, 0x00, 0x01 });

                    DHCPServer.Send(Message.ToByteArray(), Message.ToByteArray().Length, new IPEndPoint(IPAddress.Broadcast, 68));
                    AppendTextBox(string.Format("Sent DHCP Offer to {0} of {1}", Message.MAC.ToString(), Message.YourIP.ToString()));
                }
                else if (Message.Options.OptionValues.Find(x => x.OptionID == 53).Data[0] == 0x03)
                {
                    AppendTextBox(string.Format("Got DHCP Request from {0} for {1}", Message.MAC.ToString(), Message.YourIP.ToString()));

                    Message.Operation = DHCPMessage.OperationType.REPLY;
                    Message.Hops = 0;
                    Message.YourIP = new IPAddress(new byte[4] { 0x0a, 0x00, 0x00, 0x02 });
                    Message.ServerIP = new IPAddress(new byte[4] { 0x0a, 0x00, 0x00, 0x01 });
                    Message.GatewayIP = new IPAddress(new byte[4] { 0x0a, 0x00, 0x00, 0x01 });
                    Message.Options = new DHCPOptions();

                    Message.Options.AddOptionValue(DHCPOptions.DHCPOptionValue.DHCPOptionIDs.SubnetMask, new byte[] { 0xff, 0x00, 0x00, 0x00 });
                    Message.Options.AddOptionValue(DHCPOptions.DHCPOptionValue.DHCPOptionIDs.Gateway, new byte[] { 0x0a, 0x00, 0x00, 0x01 });
                    Message.Options.AddOptionValue(DHCPOptions.DHCPOptionValue.DHCPOptionIDs.DNSServers, new byte[] { 0x0a, 0x00, 0x00, 0x01 });
                    Message.Options.AddOptionValue(DHCPOptions.DHCPOptionValue.DHCPOptionIDs.LeaseTime, new byte[] { 0xFF, 0xff, 0xff, 0xff });
                    Message.Options.AddOptionValue(DHCPOptions.DHCPOptionValue.DHCPOptionIDs.DHCPOffer, new byte[] { 0x05 });
                    Message.Options.AddOptionValue(DHCPOptions.DHCPOptionValue.DHCPOptionIDs.ServerIP, new byte[] { 0x0a, 0x00, 0x00, 0x01 });

                    DHCPServer.Send(Message.ToByteArray(), Message.ToByteArray().Length, new IPEndPoint(IPAddress.Broadcast, 68));
                    AppendTextBox(string.Format("Sent DHCP ACK to {0} for {1}", Message.MAC.ToString(), Message.YourIP.ToString()));
                    }
                    State.UDPClient.BeginReceive(new AsyncCallback(DHCPHandleAsync), State);
                }
            }
            
        }
        
        private class DHCPMessage
        {
            public OperationType Operation = OperationType.UNKNOWN; //Operation Type, Request or Response | 1 Byte
            public HardwareType HType = HardwareType.UNKNOWN; //Hardware Type | 1 Byte
            public int HLen = 0; //Hardware Address Length | 1 Byte
            public int Hops = 0; //Number of Forwards | 1 Byte
            public byte[] XID = new byte[4]{0x00,0x00,0x00,0x00}; //Transaction Identifier | 4 Bytes
            public int Secs = 0; //Seconds Since First Request | 2 Bytes
            public DHCPFlags Flags = new DHCPFlags(); //DHCP Flags | 2 Bytes
            public IPAddress ClientIP = IPAddress.None; //Client IP used for BOUND, RENEWING, REBINDING | 4 Bytes
            public IPAddress YourIP = IPAddress.None; //IP for Client to Bind to | 4 Bytes
            public IPAddress ServerIP = IPAddress.None; //Server IP Address | 4 Bytes
            public IPAddress GatewayIP = IPAddress.None; //Default Gateway IP | 4 Bytes
            public PhysicalAddress MAC = PhysicalAddress.None; //Client Mac Address | 16 Bytes
            public string ServerName = ""; //DHCP Server Name | 64 Bytes
            public string BootFile = ""; //TFTP Boot File Name | 128 Bytes
            public DHCPOptions Options = new DHCPOptions(); //DHCP Options | Variable Length

            public byte[] ToByteArray()
            {
                byte[] Base = new byte[240];
                Base[0] = (byte)Operation;
                Base[1] = (byte)HType;
                Base[2] = (byte)HLen;
                Base[3] = (byte)Hops;
                Array.Copy(XID, 0, Base, 4, 4);
                Base[8] = 0x00;
                Base[9] = (byte)Secs;
                Array.Copy(Flags.ToByteArray(), 0, Base, 10, 2);
                Array.Copy(ClientIP.GetAddressBytes(), 0, Base, 12, 4);
                Array.Copy(YourIP.GetAddressBytes(), 0, Base, 16, 4);
                Array.Copy(ServerIP.GetAddressBytes(), 0, Base, 20, 4);
                Array.Copy(GatewayIP.GetAddressBytes(), 0, Base, 24, 4);
                byte[] MacArray = MAC.GetAddressBytes();
                if (MacArray.Length < 16)
                {
                    Array.Resize(ref MacArray, 16);
                }
                Array.Copy(MacArray, 0, Base, 28, 16);
                Array.Copy(GetBytes(ServerName), 0, Base, 44, 64);
                Array.Copy(GetBytes(BootFile), 0, Base, 108, 128);

                byte[] MagicFlag = new byte[] { 0x63, 0x82, 0x53, 0x63 };

                Array.Copy(MagicFlag, 0, Base, 236, 4);

                byte[] OptArray = Options.ToByteArray();

                Array.Resize<byte>(ref Base, 240+OptArray.Length);
                Array.Copy(OptArray,0,Base,240,OptArray.Length);

                return Base;
            }

            public static DHCPMessage Parse(byte[] Message)
            {
                DHCPMessage Result = new DHCPMessage();
                Result.Operation = (OperationType)Message[0];
                Result.HType = (HardwareType)Message[1];
                Result.HLen = Convert.ToInt32(Message[2]);
                Result.Hops = Convert.ToInt32(Message[3]);
                Array.Copy(Message, 4, Result.XID, 0, 4);
                Result.Secs = (Convert.ToInt32(Message[8])*256) + (Convert.ToInt32(Message[9]));
                Result.Flags = DHCPFlags.Parse(Message.SubArray(10, 2));
                Result.ClientIP = new IPAddress(Message.SubArray(12, 4));
                Result.YourIP = new IPAddress(Message.SubArray(16, 4));
                Result.ServerIP = new IPAddress(Message.SubArray(20, 4));
                Result.GatewayIP = new IPAddress(Message.SubArray(24, 4));
                Result.MAC = new PhysicalAddress(Message.SubArray(28, Result.HLen));
                Result.ServerName = System.Text.Encoding.Default.GetString(Message.SubArray(44, 64));
                Result.BootFile = System.Text.Encoding.Default.GetString(Message.SubArray(108, 128));

                if (Message.Length > 236)
                {
                    Result.Options = DHCPOptions.Parse(Message.SubArray(240, (Message.Length - 240)));
                }

                return Result;
            }

            

            private byte[] GetBytes(string str)
            {
                byte[] bytes = new byte[str.Length * sizeof(char)];
                System.Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
                return bytes;
            }

            public enum OperationType
            {
                UNKNOWN = 0,
                REQUEST = 1,
                REPLY = 2
            }
            public enum HardwareType
            {
                UNKNOWN = 0,
                ETHERNET = 1,
                IEEE802 = 6,
                ARCNET = 7,
                LOCALTALK = 11,
                LOCALNET = 12,
                SMDS = 14,
                FRAMERELAY = 15,
                ATM = 16,
                HDLC = 17,
                FIBRECHANNEL = 18,
                ATM2 = 19,
                SERIAL = 20
            }
        }

        private class DHCPOptions
        {
            public List<DHCPOptionValue> OptionValues = new List<DHCPOptionValue>();

            public void AddOptionValue(DHCPOptionValue.DHCPOptionIDs Option, byte[] Data)
            {
                DHCPOptionValue value = new DHCPOptionValue();
                value.OptionID = (int)Option;
                value.Data = Data;
                OptionValues.Add(value);
            }

            public byte[] ToByteArray()
            {
                OptionValues.OrderBy(x => x.Data);
                List<byte> Result = new List<byte>();
                foreach (DHCPOptionValue Opt in OptionValues)
                {
                    Result.Add((byte)Opt.OptionID);
                    Result.Add((byte)Opt.Data.Length);
                    Result.AddRange(Opt.Data);
                }
                return Result.ToArray();
            }

            public static DHCPOptions Parse(byte[] Opts)
            {
                DHCPOptions Result = new DHCPOptions();
                DHCPOptionValue CurrentOption;
                int OptionLength = 0;
                Queue<Byte> ByteQueue = new Queue<byte>(Opts);
                while (ByteQueue.Count > 0)
                {
                    CurrentOption = new DHCPOptionValue();
                    CurrentOption.OptionID = Convert.ToInt32(ByteQueue.Dequeue());
                    if (CurrentOption.OptionID == 255)
                    {
                        return Result;
                    }
                    OptionLength = Convert.ToInt32(ByteQueue.Dequeue());
                    CurrentOption.Data = new byte[OptionLength];
                    for (int i = 0; i < OptionLength; i++)
                    {
                        CurrentOption.Data[i] = ByteQueue.Dequeue();
                    }
                    Result.OptionValues.Add(CurrentOption);
                }
                return Result;
            }

            public class DHCPOptionValue
            {
                public int OptionID = 0;
                public byte[] Data;

                

                public enum DHCPOptionIDs
                {
                    DHCPOffer = 53,
                    SubnetMask = 1,
                    Gateway = 3,
                    LeaseTime = 51,
                    ServerIP = 54,
                    DNSServers = 6
                }
            }
        }

        private class DHCPFlags //DHCP Flags Field | 2 Bytes
        {
            public bool Broadcast = false;

            public byte[] ToByteArray()
            {
                if (Broadcast) { return new byte[] { 0xF0, 0x00 };}
                else { return new byte[] { 0x00, 0x00 }; }
            }

            public static DHCPFlags Parse(byte[] Flags)
            {
                DHCPFlags Result = new DHCPFlags();
                if (Flags == new byte[] { 0xF0, 0x00 })
                {
                    Result.Broadcast = true;
                }
                return Result;
            }
        }

        #endregion DHCP

        #region DNS

        private void DoDNS()
        {
            DNSServer = new UdpClient(new IPEndPoint((IPAddress)cmbIP.SelectedItem, 53));
            IPEndPoint Client = new IPEndPoint(IPAddress.Any, 0);
            UdpState State = new UdpState();
            State.UDPClient = DNSServer;
            State.EndPoint = Client;
            State.BindAddr = (IPAddress)cmbIP.SelectedItem;
            DNSServer.BeginReceive(new AsyncCallback(DNSHandleAsync), State);
        }

        private void DNSHandleAsync(IAsyncResult ar)
                {
                    DNSMessage Message = new DNSMessage();
                    UdpState State = (UdpState)ar.AsyncState;
                    lock (State.UDPClient)
                    {
                        if (State.UDPClient.Client != null)
                        {
                            byte[] buffer = State.UDPClient.EndReceive(ar, ref State.EndPoint);
                            Message = DNSMessage.Parse(buffer);
                            if (Message.Header.Request)
                            {
                                Message.Header.Request = false;
                                Message.Header.AnswerCount = Message.Header.QueryCount;
                                Message.Header.Authoratative = true;
                                Message.Header.RecursiveAvail = Message.Header.Recursive;
                                DNSAnswer Answer;
                                foreach (DNSQuery Query in Message.Queries)
                                {
                                    AppendTextBox(string.Format("Got DNS query for {0}", Query.QName));
                                    Answer = new DNSAnswer();
                                    Answer.AName = Query.QName;
                                    Answer.AClass = (DNSAnswer.AnswerClass)Query.QClass;
                                    Answer.AType = (DNSAnswer.AnswerType)Query.QType;
                                    Answer.Address = State.BindAddr;
                                    Message.Answers.Add(Answer);
                                }
                                DNSServer.Send(Message.GetByteArray(), Message.GetByteArray().Length, State.EndPoint);
                                AppendTextBox("Sent Answers for Last Query");
                            }
                            State.UDPClient.BeginReceive(new AsyncCallback(DNSHandleAsync), State);
                        }
                    }
                }
        
        private class DNSMessage
        {
            public DNSHeader Header = new DNSHeader();
            public List<DNSQuery> Queries = new List<DNSQuery>();
            public List<DNSAnswer> Answers = new List<DNSAnswer>();

            public static DNSMessage Parse(byte[] Data)
            {
                DNSMessage Result = new DNSMessage();
                Result.Header = DNSHeader.Parse(Data.SubArray(0, 12));
                Result.Queries = DNSQuery.Parse(Data.SubArray(12), Result.Header.QueryCount);
                return Result;
            }

            public byte[] GetByteArray()
            {
                List<byte> Response = new List<byte>();
                Response.AddRange(Header.GetByteArray());
                Response.AddRange(DNSQuery.GetByteArray(Queries));
                Response.AddRange(DNSAnswer.GetByteArray(Answers));
                return Response.ToArray();
            }
        }

        private class DNSHeader
        {
            public int ID = 0; //Identifier                                         | 16 Bits
            public bool Request = false; //Request or Response                      | 1 Bit
            public OperationType OpCode = OperationType.Unknown; // Operation Code  | 4 Bits
            public bool Authoratative = true; //Authoratative Answer                | 1 bit
            public bool Truncated = false; //Is Answer Truncated                    | 1 bit
            public bool Recursive = false; //Want Recursive results?                | 1 bit
            public bool RecursiveAvail = true; //Is Recursive Available?            | 1 bit
            public ResponseCode Response = ResponseCode.Unknown; //Error Code       | 4 Bits
            public int QueryCount = 0; //Number of Queries                          | 16 Bits
            public int AnswerCount = 0; //Number of Answers                         | 16 Bits
            public int NameServerCount = 0; //Number of NameServer Records          | 16 Bits
            public int AdditionalRecordsCount = 0; //Number of Additional Records   | 16 Bits

            public enum OperationType
            {
                Query = 0,
                iQuery = 1,
                Status = 2,
                Notify = 4,
                Update = 5,
                Unknown = 6
            }

            public enum ResponseCode
            {
                OK = 0,
                FormatError = 1,
                ServerFailure = 2,
                NameError = 3,
                NotImplemented = 4,
                Refused = 5,
                Unknown = 6
            }

            public static DNSHeader Parse(byte[] Header)
            {
                DNSHeader Result = new DNSHeader();
                Header.ConvertEndian(0, 2);
                Result.ID = BitConverter.ToInt16(Header, 0);
                Result.Request = !((Header[2] & 0x80) == 0x80);
                Result.OpCode = (OperationType)((Header[2] >> 1) & 15);
                Result.Authoratative = ((Header[2] & 0x04) == 0x04);
                Result.Truncated = ((Header[2] & 0x02) == 0x02);
                Result.Recursive = ((Header[2] & 0x01) == 0x01);
                Result.RecursiveAvail = ((Header[3] & 0x80) == 0x80);
                Result.Response = (ResponseCode)((Header[3] >> 4) & 15);
                Header.ConvertEndian(4, 2);
                Header.ConvertEndian(6, 2);
                Header.ConvertEndian(8, 2);
                Header.ConvertEndian(10, 2);
                Result.QueryCount = BitConverter.ToInt16(Header, 4);
                Result.AnswerCount = BitConverter.ToInt16(Header, 6);
                Result.NameServerCount = BitConverter.ToInt16(Header, 8);
                Result.AdditionalRecordsCount = BitConverter.ToInt16(Header, 10);

                return Result;

            }

            public byte[] GetByteArray()
            {
                byte[] Result = new byte[12];

                Array.Copy(BitConverter.GetBytes((short)ID), 0, Result, 0, 2);
                if (!Request) { Result[2] += 0x80; }
                Result[2] += (byte)((int)OpCode << 3);
                if (Authoratative) { Result[2] += 0x04; }
                if (Truncated) { Result[2] += 0x02; }
                if (Recursive) { Result[2] += 0x01; }
                if (RecursiveAvail) { Result[3] += 0x80; }
                Result[3] += 0x20;
                Result[3] += (byte)((int)Response);
                Array.Copy(BitConverter.GetBytes((short)QueryCount), 0, Result, 4, 2);
                Array.Copy(BitConverter.GetBytes((short)AnswerCount), 0, Result, 6, 2);
                Array.Copy(BitConverter.GetBytes((short)NameServerCount), 0, Result, 8, 2);
                Array.Copy(BitConverter.GetBytes((short)AdditionalRecordsCount), 0, Result, 10, 2);

                Result.ConvertEndian(0, 2);
                Result.ConvertEndian(4, 2);
                Result.ConvertEndian(6, 2);
                Result.ConvertEndian(8, 2);
                Result.ConvertEndian(10, 2);

                return Result;
            }
        }

        private class DNSQuery
        {
            public string QName = ""; //DNS Query, Variable length
            public QueryType QType = QueryType.A;
            public QueryClass QClass = QueryClass.INTERNET;

            public static List<DNSQuery> Parse(byte[] Data, int QueryCount)
            {
                List<DNSQuery> Result = new List<DNSQuery>();
                int position = 0;
                int length = 0;
                StringBuilder Builder = new StringBuilder();
                DNSQuery CurrentQuery;
                for (int i = 0; i < QueryCount; i++)
                {
                    CurrentQuery = new DNSQuery();
                    Builder.Clear();
                    length = Convert.ToInt32(Data[position]);
                    while (length > 0)
                    {
                        position++;
                        Builder.Append(System.Text.Encoding.Default.GetString(Data.SubArray(position, length)));
                        Builder.Append(".");
                        position += length;
                        length = Convert.ToInt32(Data[position]);
                    }
                    position++;
                    CurrentQuery.QName = Builder.ToString();
                    Data.ConvertEndian(position, 2);
                    CurrentQuery.QType = (DNSQuery.QueryType)BitConverter.ToInt16(Data, position);
                    position += 2;
                    Data.ConvertEndian(position, 2);
                    CurrentQuery.QClass = (DNSQuery.QueryClass)BitConverter.ToInt16(Data, position);
                    position += 2;
                    Result.Add(CurrentQuery);
                }
                return Result;
            }

            public static byte[] GetByteArray(List<DNSQuery> Queries)
            {
                List<byte> Response = new List<byte>();
                foreach (DNSQuery Query in Queries)
                {
                    foreach (string part in Query.QName.Split(new char[] {'.'},StringSplitOptions.RemoveEmptyEntries))
                    {
                        Response.Add((byte)part.Length);
                        Response.AddRange(System.Text.Encoding.Default.GetBytes(part));
                    }
                    Response.Add(0x00);
                    Response.AddRange(BitConverter.GetBytes((short)Query.QType).ConvertEndian(0,2));
                    Response.AddRange(BitConverter.GetBytes((short)Query.QClass).ConvertEndian(0, 2));
                }
                return Response.ToArray();
            }

            public enum QueryType
            {
                A = 1,
                NS = 2,
                CNAME = 5,
                SOA = 6
            }

            public enum QueryClass
            {
                INTERNET = 1
            }
        }

        private class DNSAnswer
        {
            public string AName = ""; //Answer Name, Variable length
            public AnswerType AType = AnswerType.A; //Answer Type
            public AnswerClass AClass = AnswerClass.INTERNET; //Answer Class
            public int TTL = 1000; //Time to live
            public IPAddress Address = IPAddress.Any;

            public static byte[] GetByteArray(List<DNSAnswer> Answers)
            {
                List<byte> Response = new List<byte>();
                foreach (DNSAnswer Answer in Answers)
                {
                    foreach (string part in Answer.AName.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        Response.Add((byte)part.Length);
                        Response.AddRange(System.Text.Encoding.Default.GetBytes(part));
                    }
                    Response.Add(0x00);
                    Response.AddRange(BitConverter.GetBytes((short)Answer.AType).ConvertEndian(0, 2));
                    Response.AddRange(BitConverter.GetBytes((short)Answer.AClass).ConvertEndian(0, 2));
                    Response.AddRange(BitConverter.GetBytes(Answer.TTL).ConvertEndian(0, 4));
                    Response.Add(0x00);
                    Response.Add(0x04);
                    Response.AddRange(Answer.Address.GetAddressBytes());
                }
                return Response.ToArray();
            }

            public enum AnswerType
            {
                A = 1,
                NS = 2,
                CNAME = 5,
                SOA = 6
            }

            public enum AnswerClass
            {
                INTERNET = 1
            }
        }
        #endregion DNS
    }

    public static class ExtensionMethods
    {
        public static T[] SubArray<T>(this T[] data, int index, int length)
        {
            T[] result = new T[length];
            Array.Copy(data, index, result, 0, length);
            return result;
        }

        public static T[] SubArray<T>(this T[] data, int index)
        {
            T[] result = new T[data.Length - index];
            Array.Copy(data, index, result, 0, data.Length - index);
            return result;
        }

        public static byte[] ConvertEndian(this byte[] data, int index, int length)
        {
            byte[] reversed = new byte[length];
            Array.Copy(data, index, reversed, 0, length);
            Array.Reverse(reversed);
            Array.Copy(reversed, 0, data, index, length);
            return data;
        }

        public static T[] Append<T>(this T[] data, T value)
        {
            Array.Resize(ref data, (data.Length + 1));
            data[data.Length - 1] = value;
            return data;
        }
    }
}
