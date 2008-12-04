﻿/*
 * Process Hacker
 * 
 * Copyright (C) 2008 wj32
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace ProcessHacker
{
    public struct HandleItem
    {
        public Win32.SYSTEM_HANDLE_INFORMATION Handle;
        public Win32.ObjectInformation ObjectInfo;
    }

    public class HandleProvider : Provider<short, HandleItem>
    {
        private int _pid;

        public HandleProvider(int PID)
            : base()
        {
            _pid = PID;
            this.ProviderUpdate += new ProviderUpdateOnce(UpdateOnce);   
        }

        private void UpdateOnce()
        {
            Win32.ProcessHandle processHandle = new Win32.ProcessHandle(_pid, Win32.PROCESS_RIGHTS.PROCESS_DUP_HANDLE);
            Win32.SYSTEM_HANDLE_INFORMATION[] handles = Win32.EnumHandles();
            Dictionary<short, Win32.SYSTEM_HANDLE_INFORMATION> processHandles = 
                new Dictionary<short, Win32.SYSTEM_HANDLE_INFORMATION>();
            Dictionary<short, Win32.ObjectInformation> processHandlesInfo =
                new Dictionary<short, Win32.ObjectInformation>();
            Dictionary<short, HandleItem> newdictionary = new Dictionary<short, HandleItem>();

            foreach (short key in Dictionary.Keys)
                newdictionary.Add(key, Dictionary[key]);

            foreach (Win32.SYSTEM_HANDLE_INFORMATION handle in handles)
            {
                if (handle.ProcessId == _pid)
                {
                    Win32.OBJECT_NAME_INFORMATION oni;
                    Win32.ObjectInformation info;

                    try
                    {
                        oni = Win32.GetHandleName(processHandle, handle);

                        if ((oni.Name.Buffer == null ||
                            oni.Name.Buffer == ""))
                            continue;

                        info = Win32.GetHandleInfo(processHandle, handle);

                        //if ((info.BestName == null ||
                        //    info.BestName == "") &&
                        //    Properties.Settings.Default.HideHandlesNoName)
                        //    continue;
                    }
                    catch
                    {
                        continue;
                    }

                    processHandles.Add(handle.Handle, handle);
                    processHandlesInfo.Add(handle.Handle, info);
                }
            }

            // look for closed handles
            foreach (short h in Dictionary.Keys)
            {
                if (!processHandles.ContainsKey(h))
                {                 
                    this.CallDictionaryRemoved(this.Dictionary[h]);
                    newdictionary.Remove(h);
                }
            }

            // look for new handles
            foreach (short h in processHandles.Keys)
            {
                if (!Dictionary.ContainsKey(h))
                {
                    HandleItem item = new HandleItem();

                    item.Handle = processHandles[h];
                    item.ObjectInfo = processHandlesInfo[h];

                    newdictionary.Add(h, item);
                    this.CallDictionaryAdded(item);
                }
            }

            processHandle.Dispose();
            Dictionary = newdictionary;
        }
    }
}
