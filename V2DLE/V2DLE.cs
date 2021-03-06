﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;

namespace Comm
{


  

    public class V2DLE : Comm.I_DLE
    {
     public    const   byte DLE = 0x10;
        public const byte SOH = 0x01;
        public const byte ACK = 0x06;
        public const byte NAK = 0x15;
        public const int T0ms = 5000;  //Controller 上傳等待TimeOut 時間
        public const int T1ms = 6000;  //中央電腦 要求資料上傳 等待TimeOut 時間
        public const int T2ms = 6000;//中央電腦 資料下載 等待TimeOut 時間
        public const int PackageSendTimeOut = 20000;
        public static byte DLE_ERR_PARITY_BIT_NO =0;
        public static byte DLE_ERR_FRAME = 0x01;
        public static byte DLE_ERR_LCR = 0x02;
        public static byte DLE_ERR_TIMEOUT = 0x03;
        public static byte DLE_ERR_CMD_ERR = 0x4;
        public static byte DLE_ERR_CMD_PARAM_OVERRANGE = 0x5;
        public static byte DLE_ERR_CMD_FAIL = 0x6;
        
        public event OnCommErrHandler OnCommError;
        public event OnAckEventHandler OnAck;
        public event OnNakEventHandler OnNak;
        public event OnTextPackageEventHandler OnReceiveText;
        public event OnTextPackageEventHandler OnTextCmdCheck;
        public event OnSendingAckNakHandler OnBeforeAck;
        public event OnSendingAckNakHandler OnBeforeNak;
        public event OnTextPackageEventHandler OnReport;
        public event OnSendPackgaeHandler OnSendingPackage;
        public event OnSendingAckNakHandler OnTextSending;
       // public event OnSendingAckNakHandler OnSendingAck;
       

        
           private bool Enabled=true;

      //  public  ISCmdValidCheckTemplate  IsValidCCmdFuncPtr;  //chk cmd range valid function
        byte Seq = 0; //0~127
        System.Collections.Queue SendQueueA = System.Collections.Queue.Synchronized(new System.Collections.Queue(100));
        System.Collections.Queue SendQueueB = System.Collections.Queue.Synchronized(new System.Collections.Queue(100));
        System.Collections.Queue SendQueueC = System.Collections.Queue.Synchronized(new System.Collections.Queue(100));
        System.Collections.Queue SendQueueD = System.Collections.Queue.Synchronized( new System.Collections.Queue(100));
        System.Collections.Queue ReceiveQueue = System.Collections.Queue.Synchronized(new System.Collections.Queue(100));
        System.Collections.Queue TimeOutQueue = System.Collections.Queue.Synchronized(new System.Collections.Queue(100));
        private string m_devName;
        System.IO.Stream stream;
        SendPackage currentSendingPackage = null;
        Object currentRespObj = null;

        byte[] AckData = new byte[6];
        byte[] NakData = new byte[8];
        System.Threading.Thread SendTaskThread ;
        System.Threading.Thread ReceiveTaskThread;
        object CurrSendingproxyobj = new object();
        public V2DLE(string devName,System.IO.Stream stream)
        {
            this.m_devName = devName;
            this.stream=stream;
            SendTaskThread = new System.Threading.Thread(SendTask);
            ReceiveTaskThread = new System.Threading.Thread(ReceiveTask);
            SendTaskThread.Start();
            ReceiveTaskThread.Start();


        }

        public string getDeviceName()
        {
            return this.m_devName;
        }

        public void setDeviceName(string devName)
        {
            this.m_devName = devName;
        }
        public void setEnable(bool enable)
        {
            this.Enabled = enable;
        }

        public bool getEnable()
        {
            return this.Enabled;
        }


        public string getQueueState()
        {

            return "SendQueueA=" + SendQueueA.Count + "\r\n" + "SendQueueB=" + SendQueueB.Count + "\r\n" + "SendQueueC=" + SendQueueC.Count + "\r\n" + "SendQueueD=" + SendQueueD.Count +
                "\r\n" + "ReceiveQueue=" + ReceiveQueue.Count + "\r\n" + "TimeOutQueue=" + TimeOutQueue.Count;
            //Console.WriteLine();
            //Console.WriteLine();
            //Console.WriteLine("SendQueueC=" + SendQueueC.Count);
            //Console.WriteLine("SendQueueD=" + SendQueueD.Count);
            //Console.WriteLine("ReceiveQueue=" + ReceiveQueue.Count);
            //Console.WriteLine("TimeOutQueue" + TimeOutQueue.Count);
        }

        public int getTotalQueueCnt()
        {
            return SendQueueA.Count + SendQueueB.Count + SendQueueC.Count + SendQueueD.Count + TimeOutQueue.Count;
        }

        public bool IsOnAckRegisted()
        {
            return this.OnAck != null;
        }

        public int GeTimeOutQueueCount()
        {
            return TimeOutQueue.Count;
        }
        public void  Send(SendPackage pkg)
        {

            if(!this.getEnable())
                throw new Exception("Comm  not initial!");
            switch (pkg.cls)
            {
                case CmdClass.A:
                    lock (pkg)
                    {
                   
                        lock (SendTaskNotifyObj)
                        {
                            SendQueueA.Enqueue(pkg);
                            System.Threading.Monitor.Pulse(SendTaskNotifyObj);
                        }
                        if (!System.Threading.Monitor.Wait(pkg, PackageSendTimeOut))
                        {
                            
                            pkg.result = CmdResult.TimeOut;
                            pkg.IsDiscard = true;
                            throw new Exception(string.Format(this.m_devName + ",Queue Waiting Exceed {0} sec!" + pkg, PackageSendTimeOut / 1000 + "\r\n" )+ this.getQueueState());
                        }
                    }
                  
                    break;
                case CmdClass.B:
                    lock (pkg)
                    {
                       
                        lock (SendTaskNotifyObj)
                        {
                            SendQueueB.Enqueue(pkg);
                            System.Threading.Monitor.Pulse(SendTaskNotifyObj);
                        }
                        if (!System.Threading.Monitor.Wait(pkg, PackageSendTimeOut))
                        {
                            pkg.result = CmdResult.TimeOut;
                            pkg.IsDiscard = true;
                            throw new Exception(string.Format(this.m_devName + ",Queue Waiting Exceed {0} sec!" + pkg, PackageSendTimeOut / 1000) + "\r\n"+this.getQueueState());
                        }
                    }
                    break;
                case CmdClass.C:
                    lock (pkg)
                    {
                      
                        lock (SendTaskNotifyObj)
                        {
                            SendQueueC.Enqueue(pkg);
                            System.Threading.Monitor.Pulse(SendTaskNotifyObj);
                        }

                        if (!System.Threading.Monitor.Wait(pkg, PackageSendTimeOut))
                        {
                            pkg.result = CmdResult.TimeOut;
                            pkg.IsDiscard = true;
                            throw new Exception(string.Format(this.m_devName + ",Queue Waiting Exceed {0} sec!" + pkg, PackageSendTimeOut / 1000) + "\r\n" + this.getQueueState());
                        }
                    }
                    break;
                case CmdClass.D:
                    lock (pkg)
                    {

                        lock (SendTaskNotifyObj)
                        {
                            SendQueueD.Enqueue(pkg);
                            System.Threading.Monitor.Pulse(SendTaskNotifyObj);
                        }

                        if (!System.Threading.Monitor.Wait(pkg, PackageSendTimeOut))
                        {
                            pkg.result = CmdResult.TimeOut;
                            pkg.IsDiscard = true;
                            throw new Exception(string.Format(this.m_devName + ",Queue Waiting Exceed {0} sec!" + pkg, PackageSendTimeOut / 1000) + "\r\n" + this.getQueueState());
                        }
                    }
                    break;

            }
           
       //     Console.WriteLine("---------------finish pkg" + pkg);

        }

        public void Send(SendPackage[] pkgs)
        {
            foreach(SendPackage pkg in pkgs)
            {
                try{

                    Send(pkg);
                } catch{;}
            }


        }

        object SendTaskNotifyObj=new object();
        
        void SendTask()
        {

            byte[]data=null;
            while (true)
            {
                if (!this.getEnable())
                {
                    Console.WriteLine(m_devName+",send task exit!");
                    return;
                }
                try
                {
                    lock (SendTaskNotifyObj)
                    {
                       

                            if (SendQueueA.Count != 0)
                                while (SendQueueA.Count != 0)
                                {
                                    currentSendingPackage = (SendPackage)SendQueueA.Dequeue();
                                    if (!currentSendingPackage.IsDiscard)
                                        break;
                                    else
                                        currentSendingPackage = null;

                                }

                            else if (SendQueueB.Count != 0)

                                while (SendQueueB.Count != 0)
                                {
                                    currentSendingPackage = (SendPackage)SendQueueB.Dequeue();
                                    if (!currentSendingPackage.IsDiscard)
                                        break;
                                    else
                                        currentSendingPackage = null;
                                }
                            else if (SendQueueC.Count != 0)
                                while (SendQueueC.Count != 0)
                                {
                                    currentSendingPackage = (SendPackage)SendQueueC.Dequeue();
                                    if (!currentSendingPackage.IsDiscard)
                                        break;
                                    else
                                        currentSendingPackage = null;
                                }
                            else if (SendQueueD.Count != 0)
                                while (SendQueueD.Count != 0)
                                {
                                    currentSendingPackage = (SendPackage)SendQueueD.Dequeue();
                                    if (!currentSendingPackage.IsDiscard)
                                        break;
                                    else
                                        currentSendingPackage = null;
                                }
                            else if (TimeOutQueue.Count != 0)
                                while (TimeOutQueue.Count != 0)
                                {
                                    currentSendingPackage = (SendPackage)TimeOutQueue.Dequeue();
                                    if (!currentSendingPackage.IsDiscard)
                                        break;
                                    else
                                        currentSendingPackage = null;
                                }
                            else
                            {
                                //no Data To Send , and waiting

                                currentSendingPackage = null;
                                //GC.Collect();
                                //GC.WaitForPendingFinalizers();
                                System.Threading.Monitor.Wait(SendTaskNotifyObj);


                                continue;
                            }

                       


                    }



                    try
                    {

                        //lock (CurrSendingproxyobj)
                        //{
                            if (currentSendingPackage == null) continue;

                            if (currentSendingPackage.sendCnt == 0)
                                data = PackData(currentSendingPackage.address, currentSendingPackage.text);
                            else
                                data = PackData(currentSendingPackage.address, currentSendingPackage.text, (byte)currentSendingPackage.Seq);

                            currentSendingPackage.Seq = data[2];
                            if (!this.getEnable())
                                return;

                            if (currentSendingPackage.sendCnt >= 3)   //discard package
                            {
                               

                                lock (currentSendingPackage)
                                {
                                System.Threading.Monitor.Pulse(currentSendingPackage);
                                }
                                continue;
                            }
                            currentSendingPackage.sendCnt++;
                        //}
                        
                        if (OnSendingPackage != null)
                            OnSendingPackage(this, currentSendingPackage);
                        lock (stream)
                        {

                            if (OnTextSending != null)
                                OnTextSending(this, ref data);
                            //---------------------------------------------------------------------------------
                            //if (currentSendingPackage.type == CmdType.CmdSet)
                            //    Console.WriteLine("<============Sendig Seq :" + currentSendingPackage.Seq);
                            //--------------------------------------------------------------------

                            stream.Write(data, 0, data.Length);
                            stream.Flush();
                            //for (int i = 0; i < data.Length; i++)
                            //{
                            //  stream.WriteByte(data[i]);
                            //stream.Flush();
                            // }


                            if (System.Threading.Monitor.Wait(stream, T0ms))
                            {
                                //No Time Out


                                //lock (CurrSendingproxyobj)
                                //{

                                if (this.currentSendingPackage.result == CmdResult.ACK)
                                {
                                    lock (currentSendingPackage)
                                    {
                                        System.Threading.Monitor.Pulse(currentSendingPackage);

                                    }
                                }
                                else
                                {
                                    
                                        TimeOutQueue.Enqueue(currentSendingPackage);//nak ,time out
                                        currentSendingPackage = null;
                                   
                                }
                                //}


                            }
                            else
                            {
                                //Time out

                                //lock (CurrSendingproxyobj)
                                //{

                                  //  if (currentSendingPackage.result == CmdResult.Unknown)
                                        currentSendingPackage.result = CmdResult.TimeOut;

                                    
                                        TimeOutQueue.Enqueue(currentSendingPackage);
                                        this.currentSendingPackage = null;
                                    
                                //}
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (this.OnCommError != null)
                            OnCommError(this, ex);
                        if (!this.getEnable())
                        {

                            Console.WriteLine(m_devName + "send task exit");
                            return;
                        }
                        //  Console.WriteLine(ex.Message);
                    }

                }
                catch
                {
                    ;
                }
                 

                }//while

            




            


        }

        private int ReadByte(System.IO.Stream stream)
        {
            int ret;
            ret = stream.ReadByte();
            if (ret == -1)
                throw new System.IO.IOException(this.m_devName + ",DLE Read to end");
            else
                return ret;
        }
        void ReceiveTask()
        {
            int d;
            bool dle_flag = false;
            AckPackage AckPkg=null;
            NakPackage NakPkg = null;
            TextPackage TxtPkg = null;
        //    stream.ReadTimeout = 1000 * 300;
            while (true)
            {
                
                try
                {
                    
                    if(!dle_flag) 
                    while (  (d = ReadByte(stream)) != DLE ) ;
                //{
                //    if (d != -1)
                //        Console.WriteLine(d.ToString());
                //    ;
                //};
                    dle_flag = false;
                    d = ReadByte(stream);
                   
                    switch (d)
                    {
                        case SOH:
                          
                            TxtPkg=  ReadText();
                            currentRespObj = TxtPkg;
                            
                            if (OnTextCmdCheck != null)
                                OnTextCmdCheck(this,TxtPkg);

                            if (TxtPkg.HasErrors)
                            {
                                
                                    //new byte[] { DLE, V2DLE.ACK, TxtPkg.Seq, TxtPkg.Address / 256, TxtPkg.Address % 256, 0 };  //send nak
                                    NakData[0] = DLE;
                                    NakData[1] = NAK;
                                    NakData[2] = (byte)TxtPkg.Seq;
                                    NakData[3] = (byte)(TxtPkg.Address / 256);
                                    NakData[4] = (byte)(TxtPkg.Address % 256);
                                    NakData[5] = TxtPkg.Err[0];
                                    NakData[6] = TxtPkg.Err[1];
                                    NakData[7] = (byte)(NakData[0] ^ NakData[1] ^ NakData[2] ^ NakData[3] ^ NakData[4] ^ NakData[5] ^ NakData[6]);
                                    if (OnBeforeNak != null)
                                        OnBeforeNak(this, ref NakData);                                
                                    lock (stream)
                                    {
                                        stream.Write(NakData, 0, NakData.Length);
                                        stream.Flush();
                                    }
                                       
                               
                            }
                            else   //Query and Report
                            {  
                                //orig ack place
                                //2012-8-24
                                    if (this.OnReceiveText != null)
                                    {

                                        OnReceiveText(this, TxtPkg);
                                    }

                                    if (currentSendingPackage != null && currentSendingPackage.returnCmd == TxtPkg.Cmd && currentSendingPackage.returnSubCmd == TxtPkg.SubCmd  && currentSendingPackage.Seq==TxtPkg.Seq)
                                    {

                                        currentSendingPackage.ReturnTextPackage = TxtPkg;
                                        currentSendingPackage.result = CmdResult.ACK;


                                        lock (stream)
                                        {
                                            System.Threading.Monitor.Pulse(stream);
                                        }
                                        //2012-8-23
                                        break;
                                    }
                                    else
                                    {
                                        //----------------------------------------------------------------------------------------------------

                                        bool bfind = false;
                                        for (int i = 0; i < TimeOutQueue.Count; i++)
                                        {
                                            SendPackage pkg = (SendPackage)TimeOutQueue.Dequeue();
                                            if (pkg.type == CmdType.CmdQuery && pkg.returnCmd == TxtPkg.Cmd && pkg.returnSubCmd == TxtPkg.SubCmd && currentSendingPackage.Seq == TxtPkg.Seq)
                                            {

                                                pkg.ReturnTextPackage = TxtPkg;
                                                pkg.result = CmdResult.ACK;

                                                //lock (stream)
                                                //{
                                                //    System.Threading.Monitor.Pulse(stream);
                                                //}

                                                lock (pkg)
                                                {
                                                    System.Threading.Monitor.Pulse(pkg);
                                                }
                                                bfind = true;

                                            }
                                            else
                                                TimeOutQueue.Enqueue(pkg);  //put back time out queue
                                        }


                                        if (!bfind)
                                        {



                                            if (OnReport != null)
                                                OnReport(this, TxtPkg);
                                        }
                                        else
                                        {
                                            //2012-8-23
                                            break;
                                        }
                                    }
                                
                            
                                //if (this.OnReceiveText != null)
                                //{
                                  
                                //    OnReceiveText(this, TxtPkg);
                                //}
                                //------------------------------------ack----------------------------------------------------------------

                                AckData[0] = DLE;
                                AckData[1] = ACK;
                                AckData[2] = (byte)TxtPkg.Seq;
                                AckData[3] = (byte)(TxtPkg.Address / 256);
                                AckData[4] = (byte)(TxtPkg.Address % 256);
                                AckData[5] = (byte)(AckData[0] ^ AckData[1] ^ AckData[2] ^ AckData[3] ^ AckData[4]);
                                if (OnBeforeAck != null)
                                {
                                    OnBeforeAck(this, ref AckData);

                                }
                                lock (stream)
                                {
                                    stream.Write(AckData, 0, AckData.Length);
                                    stream.Flush();
                                }
                            }
                            break;
                        case ACK:

                           AckPkg=  ReadAck();
                           currentRespObj = AckPkg;

                           //lock (currentSendingPackage)
                           //{
                               //---------------------------------------------------------
                               ///  if (currentSendingPackage.type == CmdType.CmdSet)

                               //---------------------------------------------------------------
                          
                               if (currentSendingPackage != null && currentSendingPackage.Seq == AckPkg.Seq)
                               {
                                   //Console.WriteLine("======================>Receive Seq :" + AckPkg.Seq);

                                   //if (currentSendingPackage.type == CmdType.CmdSet)
                                   //{
                                       
                                      
                                           currentSendingPackage.result = CmdResult.ACK;
                                       
                                       // Console.WriteLine(currentSendingPackage.Cmd + "receive ack!");
                                       lock (stream)
                                       {
                                           System.Threading.Monitor.Pulse(stream);
                                       }
                                   //}

                               }
                               //----------------------------------------------------------------------------
                               else  //check pkg in TimeOutQueue
                               {
                                   for (int i = 0; i < TimeOutQueue.Count; i++)
                                   {

                                       SendPackage pkg = (SendPackage)TimeOutQueue.Dequeue();
                                       // Console.WriteLine("check wait queue:pkg.seq=" + pkg.Seq + ",ACK.seq=" + AckPkg.Seq);
                                       if (pkg.type == CmdType.CmdSet && AckPkg.Seq == pkg.Seq)
                                       {
                                           pkg.result = CmdResult.ACK;
                                           //lock (stream)
                                           //{
                                           //    System.Threading.Monitor.Pulse(stream);
                                           //}

                                           lock (pkg)
                                           {
                                               System.Threading.Monitor.Pulse(pkg);
                                           }

                                       }
                                       else
                                           TimeOutQueue.Enqueue(pkg);  //put back time out queue
                                   }

                               
                           }
                          
                            //-------------------------------------------------------------------------------------------

                           if (OnAck != null)
                               OnAck(this,AckPkg);

                            break;
                        case NAK:
                            NakPkg=  ReadNak();
                            if (currentSendingPackage!=null &&  currentSendingPackage.Seq == NakPkg.Seq)
                            {
                                currentSendingPackage.result = GetCmdResult(NakPkg);
                                lock (stream)
                                {
                                    System.Threading.Monitor.Pulse(stream);
                                }


                            }
                            if (OnNak != null)
                                OnNak(this,NakPkg);
                            break;
                        
                           
                        default:
                            if (d == DLE)
                            {
                                dle_flag = true;
                            }
                            break;
                    }  //end switch
                }
                catch (Exception ex)
                {
                  //  Console.WriteLine(ex.StackTrace);
                    try
                    {
                        if (OnCommError != null)
                        {
                            try
                            {

                                Console.WriteLine("receiver err1");
                                OnCommError(this, ex);
                                Console.WriteLine("receiver err2" + this.getEnable());
                                if (!this.getEnable())
                                {
                                    Console.WriteLine(m_devName + ",Rcv task exit");
                                    return;
                                }
                            }
                            catch  { ;}
                        }
                    }
                    catch { ;}
                  //  break;
                }

                
            }
        }

        CmdResult GetCmdResult(NakPackage pkg)
        {
            if (pkg.GetErrBit(V2DLE.DLE_ERR_LCR))
                return CmdResult.LRC_ERR;
            else if (pkg.GetErrBit(V2DLE.DLE_ERR_FRAME))
                return CmdResult.Frame_ERR;
            else if (pkg.GetErrBit(V2DLE.DLE_ERR_CMD_ERR))
                return CmdResult.Cmd_Invalid;
            else if (pkg.GetErrBit(V2DLE.DLE_ERR_CMD_PARAM_OVERRANGE))
                return CmdResult.Param_OverRange;
            else if (pkg.GetErrBit(V2DLE.DLE_ERR_CMD_FAIL))
                return CmdResult.Cmd_Fail;
            else
                return CmdResult.Unknown;
        }

        CmdResult GetCmdResult(TextPackage pkg)
        {
            if (pkg.GetErrBit(V2DLE.DLE_ERR_LCR))
                return CmdResult.LRC_ERR;
            else if (pkg.GetErrBit(V2DLE.DLE_ERR_FRAME))
                return CmdResult.Frame_ERR;
            else if (pkg.GetErrBit(V2DLE.DLE_ERR_CMD_ERR))
                return CmdResult.Cmd_Invalid;
            else if (pkg.GetErrBit(V2DLE.DLE_ERR_CMD_PARAM_OVERRANGE))
                return CmdResult.Param_OverRange;
            else if (pkg.GetErrBit(V2DLE.DLE_ERR_CMD_FAIL))
                return CmdResult.Cmd_Fail;
            else
                return CmdResult.Unknown;
        }
        
        public System.IO.Stream GetStream()
        {
            return stream;
        }

        private TextPackage ReadText()
        {
            int Seq=0,Address=0,Len=0,LRC=0,HeadLRC=0;
            byte[] text=null;
            TextPackage textPackage = new TextPackage();
            Seq = ReadByte(stream);
            Address = ReadByte(stream) * 256;
            Address+= ReadByte(stream);
            Len = ReadByte(stream) * 256;
            Len += ReadByte(stream);
            HeadLRC = ReadByte(stream);
            textPackage.Seq = Seq;
            textPackage.Address = Address;
          //  textPackage.LRC = HeadLRC;

            if (HeadLRC != (((Address >> 8) & 0x00ff) ^ (Address & 0x00ff) ^ ((Len >> 8) & 0x00ff) ^ (Len & 0x00ff)))
            {
              // textPackage.HasErrors = true;
                textPackage.SetErrBit(V2DLE.DLE_ERR_FRAME, true);
                textPackage.eErrorDescription += "Hearder LRC Error!\r\n";
                //Console.WriteLine("Hearder LRC Error!");
                return textPackage;
            }
            else
            {
               
                text = new byte[Len];
                int rlen = 0;




                do
                {
                    rlen+=stream.Read(text,rlen, Len-rlen);
                    
                } while (rlen != Len);
                //for (int i = 0; i < Len; i++)
                //    text[i] = (byte)stream.ReadByte();
              
                
                //if (rlen != Len)
                //   Console.WriteLine("rLen={0}!=len={1}",rlen,Len);


                LRC=DLE ^ SOH ^ Seq ;
                for (int i = 0; i < text.Length; i++)
                    LRC ^= text[i];

                int tmp = ReadByte(stream);
                if (LRC !=tmp)// stream.ReadByte())
                {
                    textPackage.SetErrBit(V2DLE.DLE_ERR_LCR, true);
                    textPackage.eErrorDescription += "LRC Error!\r\n";
                    textPackage.Text = text;
                    Console.WriteLine("LRC Error!"+"==>"+ textPackage.ToString());
                    return textPackage;
                }
                else
                {
                   textPackage.Text=text;
                   textPackage.LRC = LRC;
                   return textPackage;
                    
                }
                
            }


        }

        private AckPackage ReadAck()
        {
            byte[] data = new byte[4];
            stream.Read(data, 0, 4);
           return new AckPackage(data);

        }

        private NakPackage ReadNak()
        {
            byte[] data = new byte[6];
            stream.Read(data, 0, 6);

            return new NakPackage(data);
        }

        public byte[] PackData(int address, byte[] text,byte seq)
        {
            byte[] data = new byte[9 + text.Length];

            data[0] = DLE;
            data[1] = SOH;
            data[2] = seq;
            data[3] = (byte)(address / 256);
            data[4] = (byte)(address % 256);
            data[5] = (byte)(text.Length / 256);
            data[6] = (byte)(text.Length % 256);
            data[7] = (byte)(data[3] ^ data[4] ^ data[5] ^ data[6]);
            for (int i = 0; i < text.Length; i++)
                data[8 + i] = text[i];

            byte LRC = 0;
            for (int i = 0; i < data.Length - 1; i++)
                LRC ^= data[i];

            data[data.Length - 1] = LRC;

            return data;
        }

        private  byte[] PackData(int address ,byte[] text)
        {
            byte[] data=new byte[9+text.Length];

            data[0] = DLE;
            data[1] = SOH;
            data[2] =  GetSeq();
            data[3] = (byte)(address / 256);
            data[4] = (byte)(address % 256);
            data[5] = (byte)(text.Length / 256);
            data[6] = (byte)(text.Length % 256);
            data[7] =(byte)( data[3] ^ data[4] ^ data[5] ^ data[6]);
            for (int i = 0; i < text.Length; i++)
                data[8 + i] = text[i];

            byte LRC=0;
            for(int i=0;i<data.Length-1;i++)
                LRC ^= data[i];

            data[data.Length - 1] = LRC;

            return data;

        }

        private byte GetSeq()
        {

            Seq = (byte)((Seq + 1) % 128);
            return Seq;
        }

        public void Close()
        {
            try
            {
                this.Enabled = false;
                //lock (stream)
                //{
                    try
                    {
                        stream.Close();
                       // stream.Dispose();
                    }
                    catch { ;}
                //}
                lock (this.SendTaskNotifyObj)
                {
                    System.Threading.Monitor.PulseAll(SendTaskNotifyObj);
                }
                lock (stream)
                {
                    System.Threading.Monitor.PulseAll(stream);
                }
               

                //try
                //{
                //    this.SendTaskThread.Abort();
                //    Console.WriteLine(devName+"Send Task IS Alive:"+SendTaskThread.IsAlive);
                //}
                //catch { ;}
                //try
                //{
                //    this.ReceiveTaskThread.Abort();
                //    Console.WriteLine(devName+"Rcv Task IS Alive:" + ReceiveTaskThread.IsAlive);
                //}
                //catch { ;}
            }
            catch { }
           
        }
        public static double uLongToDouble(ulong data)
        {
            return System.BitConverter.ToDouble(System.BitConverter.GetBytes(data), 0);
        }
        public static ulong DoubleTouLong(double data)
        {
            return System.BitConverter.ToUInt64(System.BitConverter.GetBytes(data), 0);
            //  return System.BitConverter.ToDouble(System.BitConverter.GetBytes(data), 0);
        }

        public static string ToHexString(byte[] data)
        {
            StringBuilder sb = new StringBuilder(100);
            
            for(int i=0;i<data.Length;i++)
                sb.Append(string.Format("{0:X2} ",data[i]));

            return sb.ToString();
        }
        public static  string ToHexString(byte data)
        {
            return string.Format("{0:X2}", data);
        }
        public static string ToHexString(int data)
        {
            return string.Format("{0:X4}", data);
        }

        public static byte[] getIP(string ipstr)
        {
            string[] ips = ipstr.Split(new char[] { '.' });
            byte[] ipByte = new byte[4];
            for (int i = 0; i < 4; i++)
                ipByte[i] = System.Convert.ToByte(ips[i]);

            return ipByte;

        }
         public static string CPath(string WinPath)
        {


            if (System.Environment.OSVersion.Platform == PlatformID.Win32NT)
                return WinPath;
            else
            {
              //  Console.WriteLine("Unix");
                return WinPath.Replace(@"\", @"/");
            }
        }

    }
}
