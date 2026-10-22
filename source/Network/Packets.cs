using ProtoBuf;
namespace VSMechanicalLocks;
[ProtoContract] public class FilingPacket
{
    // open, start, cut, finish, cancel, close. A session is tied to both physical held stacks.
    [ProtoMember(1)] public string Action = "";
    [ProtoMember(2)] public string Token = "";
    [ProtoMember(3)] public int Tooth;
    [ProtoMember(4)] public int Revision;
}
[ProtoContract] public class FilingStatePacket
{
    [ProtoMember(1)] public string Token = "";
    [ProtoMember(2)] public int Revision;
    [ProtoMember(3)] public byte[] Stack = Array.Empty<byte>();
}
[ProtoContract] public class LockActionPacket
{
    [ProtoMember(1)] public int X;
    [ProtoMember(2)] public int Y;
    [ProtoMember(3)] public int Z;
    [ProtoMember(4)] public int Dimension;
    [ProtoMember(5)] public long Revision;
    [ProtoMember(6)] public bool Query;
    [ProtoMember(7)] public bool Ctrl;
}
[ProtoContract] public class LockStatePacket
{
    [ProtoMember(1)] public int X;
    [ProtoMember(2)] public int Y;
    [ProtoMember(3)] public int Z;
    [ProtoMember(4)] public int Dimension;
    [ProtoMember(5)] public string Json = "null";
}
[ProtoContract] public class NoticePacket { [ProtoMember(1)] public string Code = ""; }
[ProtoContract] public class OpenKeyringPacket { }
[ProtoContract] public class KeyringContentsPacket
{
    [ProtoMember(1)] public string DialogTitle = "";
    [ProtoMember(2)] public int SlotCount;
    [ProtoMember(3)] public byte[] TreeBytes = Array.Empty<byte>();
    [ProtoMember(4)] public string InventoryId = "";
}
