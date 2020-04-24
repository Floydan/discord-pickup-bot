using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace PickupBot.Commands
{
    public class RCON
    {

        public static async Task<string> SendCommand(string rconCommand, string host, string password, int gameServerPort)
        {
            //connecting to server
            using var client = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            client.Connect(host, gameServerPort);

            var command = $"rcon {password} {rconCommand}{Environment.NewLine}{Environment.NewLine}";
            var bufferTemp = Encoding.ASCII.GetBytes(command);
            var bufferSend = new byte[bufferTemp.Length + 4];

            //intial 5 characters as per standard
            bufferSend[0] = byte.Parse("255");
            bufferSend[1] = byte.Parse("255");
            bufferSend[2] = byte.Parse("255");
            bufferSend[3] = byte.Parse("255");
            //bufferSend[4] = byte.Parse("02");
            var j = 4;
            
            foreach (var t in bufferTemp)
            {
                bufferSend[j++] = t;
            }

            var temp = Encoding.ASCII.GetString(bufferSend);

            //send rcon command and get response
            var remoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
            await client.SendAsync(bufferSend, SocketFlags.None);

            //big enough to receive response
            var bufferRec = new byte[65000];
            client.Receive(bufferRec);
            return Encoding.ASCII.GetString(bufferRec)?
                .Replace(password, "[PASSWORD]")
                .Replace("????print\n", "")
                .Replace("\0", "");
        }
    }
}
