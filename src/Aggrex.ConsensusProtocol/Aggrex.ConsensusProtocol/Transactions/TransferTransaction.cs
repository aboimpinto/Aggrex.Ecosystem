﻿using System.IO;

namespace Aggrex.ConsensusProtocol.Transaction
{
    public class TransferTransaction : BaseTransaction
    {
        public override TransactionType TransactionType => TransactionType.TransferTransaction;

        public byte[] ScriptSig { get; set; }
        public byte[] ScriptPubKey { get; set; }

        protected override void WriteProperties(BinaryWriter writer)
        {
            writer.Write(ScriptSig?.Length ?? 0);
            writer.Write(ScriptPubKey?.Length ?? 0);
        }

        protected override void ReadProperties(BinaryReader reader)
        {
            var scriptSigLength = reader.ReadInt32();
            ScriptSig = reader.ReadBytes(scriptSigLength);

            var scriptPubKeyLength = reader.ReadInt32();
            ScriptPubKey = reader.ReadBytes(scriptPubKeyLength);
        }
    }
}