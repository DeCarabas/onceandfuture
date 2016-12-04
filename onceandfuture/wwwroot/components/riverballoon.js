var React = require('react'); // N.B. Still need this because JSX.
import {
  COLUMNWIDTH,
  RIVER_COLUMN_BACKGROUND_COLOR,
} from './style'

const RiverBalloon = ({info, dispatchAction, dismiss}) => {
  const style = {
    margin: '0 auto',
    zIndex: 1000,
    position: 'absolute',
    width: '100%',
    textAlign: 'center',
  };

  let background = RIVER_COLUMN_BACKGROUND_COLOR;
  if (info.level == 'error') {
    background = 'red';
  }

  const span_style = {
    display: 'inline-block',
    borderStyle: 'solid',
    borderWidth: '0px 1px 1px 1px',
    borderRadius: '3px',
    padding: '0px 10px 3px 10px',
    backgroundColor: background,
    fontSize: 16,
    maxWidth: COLUMNWIDTH,
  };

  const link_span_style = {
    cursor: 'pointer',
    color: 'blue',
    fontWeight: 'bold',
  };

  if (!info.text) { return <div />; }

  var action_span = <span />;
  if (info.action) {
    action_span = <span style={link_span_style} onClick={() => dispatchAction(info.action)}>
      {info.action_label}
    </span>;
  }

  return <div style={style}>
    <span style={span_style}>
      <span style={link_span_style} onClick={dismiss}>x</span> {info.text} {action_span}</span>
  </div>;
};

export default RiverBalloon;
