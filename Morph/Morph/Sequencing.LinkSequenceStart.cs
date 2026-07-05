using Morph.Base;
using Morph.Core;

namespace Morph.Sequencing
{
    public abstract class LinkSequenceStart : LinkSequence
    {
        public LinkSequenceStart(int sequenceID, int senderID, bool IsLossless)
          : base()
        {
            _sequenceID = sequenceID;
            _senderID = senderID;
            _IsLossless = IsLossless;
        }

        protected int _sequenceID;
        public int SequenceID
        {
            get => _sequenceID;
        }

        protected int _senderID;
        public int SenderID
        {
            get => _senderID;
        }

        protected bool _IsLossless;
        public bool IsLossless
        {
            get => _IsLossless;
        }

        protected abstract bool ToSender
        {
            get;
        }

        #region Link

        public override void Write(MorphWriter writer)
        {
            writer.WriteLinkByte(LinkTypeID, true, ToSender, _IsLossless);
            writer.WriteInt32(_sequenceID);
            writer.WriteInt32(_senderID);
        }

        #endregion

        public override string ToString()
        {
            string str = "{Sequence";
            str += " SequenceID=" + _sequenceID.ToString();
            str += " SenderID=" + _senderID.ToString();
            if (_IsLossless)
                str += " Lossless";
            else
                str += " Lossy";
            return str + '}';
        }
    }

    public class LinkSequenceStartSend : LinkSequenceStart
    {
        public LinkSequenceStartSend(int sequenceID, int senderID, bool isLossless)
          : base(sequenceID, senderID, isLossless)
        {
        }

        protected override bool ToSender
        {
            get => false;
        }

        public override object FindLinkObject()
        {
            return SequenceReceivers.Find(_sequenceID);
        }

        public override void Action(LinkMessage message)
        {
            if (_senderID == 0)
                throw new EMorph("At sequence, SenderID cannot be 0.");
            SequenceReceiver sequence;
            if (_sequenceID == 0)
            { //  Start new sequence
                sequence = SequenceReceivers.New(_IsLossless);
                //  The receiver replies (acks/resend requests) back along the sender's path
                if (message.PathFrom != null)
                    sequence.PathToProxy = message.PathFrom.Clone();
            }
            else
            { //  Find existing sequence
                sequence = SequenceReceivers.Find(_sequenceID);
                if (sequence == null)
                    throw new EMorph("SequenceID " + _sequenceID.ToString() + " does not exist.");
                sequence.IsLossless = _IsLossless || sequence.IsLossless;
                if (message.PathFrom != null)
                    sequence.PathToProxy = message.PathFrom.Clone();
            }
            //  Set the sender's ID
            sequence.SenderID = _senderID;
            //  Done
            message.NextLinkAction();
        }
    }

    public class LinkSequenceStartReply : LinkSequenceStart
    {
        public LinkSequenceStartReply(int sequenceID, int senderID, bool isLossless)
          : base(sequenceID, senderID, isLossless)
        {
        }

        protected override bool ToSender
        {
            get => true;
        }

        public override object FindLinkObject()
        {
            return SequenceSenders.Find(_senderID);
        }

        public override void Action(LinkMessage message)
        {
            if (message.PathTo.Peek() == this)
                message.PathTo.Pop();
            if (_sequenceID == 0)
                throw new EMorph("At sequence sender, SequenceID cannot be 0.");
            SequenceSender Sequence;
            if (_senderID == 0)
            { //  Start new sequence
                Sequence = SequenceSenders.New(message.PathFrom, _IsLossless);
                _senderID = Sequence.SenderID;
            }
            else
            { //  Find existing sequence
                Sequence = SequenceSenders.Find(_senderID);
                if (Sequence == null)
                    throw new EMorph("Sequence SenderID " + _senderID.ToString() + " does not exist.");
                Sequence.IsLossless = _IsLossless || Sequence.IsLossless;
            }
            //  Set the sender's ID
            Sequence.SequenceID = _sequenceID;
        }
    }
}