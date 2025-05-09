﻿using Newtonsoft.Json;

namespace SyncTrayzor.Syncthing.ApiClient
{
    public class DeviceDisconnectedEventData
    {
        [JsonProperty("error")]
        public string Error { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }
    }

    public class DeviceDisconnectedEvent : Event
    {
        [JsonProperty("data")]
        public DeviceDisconnectedEventData Data { get; set; }

        public override bool IsValid => Data != null &&
            !string.IsNullOrWhiteSpace(Data.Id);

        public override void Visit(IEventVisitor visitor)
        {
            visitor.Accept(this);
        }

        public override string ToString()
        {
            return $"<Disconnected ID={Id} Time={Time} Error={Data.Error} Id={Data.Id}>";
        }
    }
}
