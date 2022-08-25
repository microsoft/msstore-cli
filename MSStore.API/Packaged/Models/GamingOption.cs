// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace MSStore.API.Packaged.Models
{
    public class GamingOption
    {
        public List<string>? Genres { get; set; }
        public bool IsLocalMultiplayer { get; set; }
        public bool IsLocalCooperative { get; set; }
        public bool IsOnlineMultiplayer { get; set; }
        public bool IsOnlineCooperative { get; set; }
        public bool IsBroadcastingPrivilegeGranted { get; set; }
        public bool IsCrossPlayEnabled { get; set; }
        public string? KinectDataForExternal { get; set; }
    }
}
