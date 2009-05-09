﻿/*
 * Process Hacker - 
 *   network provider
 * 
 * Copyright (C) 2009 wj32
 * 
 * This file is part of Process Hacker.
 * 
 * Process Hacker is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * Process Hacker is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with Process Hacker.  If not, see <http://www.gnu.org/licenses/>.
 */

using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using ProcessHacker.Native;

namespace ProcessHacker
{
    public class NetworkProvider : Provider<string, NetworkConnection>
    {
        public NetworkProvider()
            : base()
        {
            this.Name = this.GetType().Name;
            this.ProviderUpdate += new ProviderUpdateOnce(UpdateOnce);   
        }

        private void UpdateOnce()
        {
            var networkDict = Windows.GetNetworkConnections();
            var preKeyDict = new Dictionary<string, KeyValuePair<int, NetworkConnection>>();
            var keyDict = new Dictionary<string, NetworkConnection>();
            var newDict = new Dictionary<string, NetworkConnection>(this.Dictionary);

            // flattens list, assigns IDs and counts
            foreach (var list in networkDict.Values)
            {
                foreach (var connection in list)
                {
                    if (connection.Pid == Program.CurrentProcessId &&
                        Properties.Settings.Default.HideProcessHackerNetworkConnections)
                        continue;

                    string id = connection.Pid.ToString() + "-" + connection.Local.ToString() + "-" +
                        (connection.Remote != null ? connection.Remote.ToString() : "") + "-" + connection.Protocol.ToString();

                    if (preKeyDict.ContainsKey(id))
                        preKeyDict[id] = new KeyValuePair<int, NetworkConnection>(
                            preKeyDict[id].Key + 1, preKeyDict[id].Value);
                    else
                        preKeyDict.Add(id, new KeyValuePair<int, NetworkConnection>(1, connection));
                }
            }

            // merges counts into IDs
            foreach (string s in preKeyDict.Keys)
            {
                var connection = preKeyDict[s].Value;

                connection.Id = s + "-" + preKeyDict[s].Key.ToString();
                keyDict.Add(s + "-" + preKeyDict[s].Key.ToString(), connection);
            }

            foreach (var connection in Dictionary.Values)
            {
                if (!keyDict.ContainsKey(connection.Id))
                {
                    lock (Dictionary)
                    {
                        OnDictionaryRemoved(connection);   
                        newDict.Remove(connection.Id);
                    }
                }
            }

            foreach (var connection in keyDict.Values)
            {
                if (!Dictionary.ContainsKey(connection.Id))
                {
                    NetworkConnection newConnection = connection;

                    newConnection.Tag = this.RunCount;
                    newDict.Add(newConnection.Id, newConnection);
                    OnDictionaryAdded(newConnection);

                    // resolve the IP addresses
                    if (newConnection.Local != null)
                    {
                        if (newConnection.Local.Address.ToString() != "0.0.0.0")
                        {
                            WorkQueue.GlobalQueueWorkItemTag(
                                new Action<string, bool, IPAddress>(this.ResolveAddresses),
                                "network-resolve",
                                newConnection.Id,
                                false,
                                newConnection.Local.Address
                                );
                        }
                    }
                    if (newConnection.Remote != null)
                    {
                        if (newConnection.Remote.Address.ToString() != "0.0.0.0")
                        {
                            WorkQueue.GlobalQueueWorkItemTag(
                                new Action<string, bool, IPAddress>(this.ResolveAddresses),
                                "network-resolve",
                                newConnection.Id,
                                true,
                                newConnection.Remote.Address
                                );
                        }
                    }
                }
                else
                {
                    if (connection.State != Dictionary[connection.Id].State)
                    {
                        lock (Dictionary)
                        {
                            var newConnection = Dictionary[connection.Id];

                            newConnection.State = connection.State;

                            OnDictionaryModified(Dictionary[connection.Id], newConnection);
                            Dictionary[connection.Id] = connection;
                        }
                    }
                }
            }

            Dictionary = newDict;
        }

        private void ResolveAddresses(string id, bool remote, IPAddress address)
        {
            IPHostEntry entry = null;

            try
            {
                entry = Dns.GetHostEntry(address);
            }
            catch (SocketException)
            {
                // Host was not found.
                return;
            }

            if (Dictionary.ContainsKey(id))
            {
                lock (Dictionary)
                {
                    if (Dictionary.ContainsKey(id))
                    {
                        try
                        {
                            var modConnection = Dictionary[id];

                            if (remote)
                                modConnection.RemoteString = entry.HostName;
                            else
                                modConnection.LocalString = entry.HostName;

                            OnDictionaryModified(Dictionary[id], modConnection);
                            Dictionary[id] = modConnection;
                        }
                        catch { }
                    }
                }
            }
        }
    }
}
