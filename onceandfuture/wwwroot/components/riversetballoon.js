var React = require('react'); // N.B. Still need this because JSX.
import { connect } from 'react-redux'
import { dismissBalloon } from '../actions'
import {
  RIVER_COLUMN_BACKGROUND_COLOR,
} from './style'

const RiverSetBalloonBase = ({info, dispatch, dismiss}) => {    
  const style = {
    margin: '0 auto',
    zIndex: 1000,
    position: 'absolute',
    width: '100%',
    textAlign: 'center',
  };

  const span_style = {
    display: 'inline-block',
    borderStyle: 'solid',
    borderWidth: '0px 1px 1px 1px',
    borderRadius: '3px',
    padding: '0px 10px 3px 10px',
    backgroundColor: RIVER_COLUMN_BACKGROUND_COLOR,
    fontSize: 16,
  };

  const link_span_style = {
    cursor: 'pointer',
    color: 'blue',
    fontWeight: 'bold',
  };

  if (!info.text) { return <div />; }
  
  var action_span = <span />;
  if (info.action) {
    action_span = <span style={link_span_style} onClick={() => dispatch(info.action)}>
      {info.action_label}
    </span>;    
  }

  return <div style={style}>
    <span style={span_style}>
      <span style={link_span_style} onClick={dismiss}>x</span> {info.text} {action_span}</span>
  </div>;
};

const mapStateToProps = (state) => {
  return {
    info: state.top_info,
  }
};
const mapDispatchToProps = (dispatch) => {
  return {
    dispatch: dispatch,
    dismiss: () => dispatch(dismissBalloon()),
  };
};
const RiverSetBalloon = connect(
  mapStateToProps,
  mapDispatchToProps
)(
  RiverSetBalloonBase
);


export default RiverSetBalloon;