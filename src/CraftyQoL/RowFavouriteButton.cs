using CraftyQoL.Core;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CraftyQoL
{
    public sealed class RowFavouriteButton : Button
    {
        private RowClickBinding? _binding;
        private long _pressed=-1;
        private int _pointer;
        internal void Initialise(RowClickBinding binding)
        {
            _binding=binding;
            onClick=new ButtonClickedEvent();
            onClick.AddListener(InvokeCurrent);
        }
        private void InvokeCurrent(){if(_binding!=null)_binding.Invoke(_binding.Version);}
        public override void OnPointerDown(PointerEventData data)
        {
            if(data.button==PointerEventData.InputButton.Left && IsActive() && IsInteractable())
            {_pressed=_binding?.Version??-1;_pointer=data.pointerId;}
            base.OnPointerDown(data);
        }
        public override void OnPointerClick(PointerEventData data)
        {
            bool current=_binding!=null && _pressed==_binding.Version && _pointer==data.pointerId;
            _pressed=-1;
            if(current)base.OnPointerClick(data);
        }
        protected override void OnDisable(){_pressed=-1;base.OnDisable();}
        // Keyboard/controller submission retains Button.OnSubmit and its normal tint feedback.
    }
}
