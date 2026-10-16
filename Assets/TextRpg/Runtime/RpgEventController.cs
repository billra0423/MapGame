using UnityEngine;
using UnityEngine.Events;

namespace MapGame.TextRpg
{
    // Attach to a scene object; bind your existing scene UI to these methods/events.
    public sealed class RpgEventController : MonoBehaviour
    {
        [SerializeField] private RpgEventMap eventMap;
        [SerializeField] private RpgPlayerState player = new RpgPlayerState();
        [SerializeField, Range(0, 14)] private int column;
        [SerializeField, Range(1, 10)] private int row = 1;
        [SerializeField] private bool allowDiagonal = true;
        [SerializeField] private bool openStartingEvent = true;
        [SerializeField] private UnityEvent onEventOpened = new UnityEvent();
        [SerializeField] private UnityEvent onEventResolved = new UnityEvent();
        [SerializeField] private UnityEvent onRejected = new UnityEvent();
        [SerializeField] private UnityEvent onPositionChanged = new UnityEvent();
        private RpgEventSession session;
        private RpgMapTravel travel;

        public RpgPlayerState Player => player;
        public RpgEvent CurrentEvent => Session.ActiveEvent;
        public string Narrative => Session.LastNarrative;
        public string LastError { get; private set; }
        public int Column => travel == null ? column : travel.Column;
        public int Row => travel == null ? row : travel.Row;
        public bool AllowDiagonal => allowDiagonal;
        public RpgEventSession Session => session ?? (session = new RpgEventSession(player));

        private void Start()
        {
            onPositionChanged.Invoke();
            if (openStartingEvent) EnterCurrentCell();
        }

        public bool IsNeighbour(int targetColumn, int targetRow)
        {
            EnsureTravel();
            return travel.IsNeighbour(targetColumn, targetRow);
        }

        private void EnsureTravel()
        {
            if (travel == null) travel = new RpgMapTravel(Session, eventMap, column, row, allowDiagonal);
        }

        public bool MoveToCell(int targetColumn, int targetRow)
        {
            EnsureTravel();
            if (!travel.TryMoveTo(targetColumn, targetRow, out string error)) { Reject(error); return false; }
            column = travel.Column;
            row = travel.Row;
            LastError = null;
            onPositionChanged.Invoke();
            if (Session.HasPendingChoice) onEventOpened.Invoke();
            else onEventResolved.Invoke();
            return true;
        }

        public void EnterCurrentCell() { EnterCell(column, row); }
        public bool EnterCell(int targetColumn, int targetRow)
        {
            if (!Session.TryEnter(eventMap, targetColumn, targetRow, out string error))
            { Reject(error); return false; }
            column = targetColumn;
            row = targetRow;
            travel = new RpgMapTravel(Session, eventMap, column, row, allowDiagonal);
            LastError = null;
            onPositionChanged.Invoke();
            onEventOpened.Invoke();
            return true;
        }
        public void Choose(int index)
        {
            if (!Session.TryChoose(index, out string error)) { Reject(error); return; }
            LastError = null;
            onEventResolved.Invoke();
        }
        public void UseItem(RpgItem item)
        {
            if (!Session.TryUseItem(item, out string error)) { Reject(error); return; }
            LastError = null;
            onEventOpened.Invoke();
        }
        private void Reject(string error) { LastError = error; onRejected.Invoke(); }
    }
}
