var React = require('react'); // N.B. Still need this because JSX.
import { COLOR_LIGHT, PROGRESS_HEIGHT } from './style';

const RiverProgress = ({progress, backgroundColor}) => {
  backgroundColor = backgroundColor || COLOR_LIGHT;

  const div_style = {
    height: PROGRESS_HEIGHT,
    backgroundColor: backgroundColor,
    width: '100%',
    zIndex: 1,
    position: 'absolute',
  };

  const percent = String(progress * 100)+'%';
  const span_style = {
    display: 'block',
    height: '100%',
    width: percent,
    position: 'relative',
    overflow: 'hidden',
    backgroundColor: COLOR_LIGHT,
  };
  const candy_style = {
    position: 'absolute',
    width: percent,
    top: 0, left: 0, bottom: 0, right: 0,
    zIndex: 2,
    overflow: 'hidden',
    backgroundSize: '50px 50px',
    animation: 'move-progress 2s linear infinite',
    backgroundImage:
      'linear-gradient(-45deg,'+
      'rgba(255, 255, 255, .2) 25%,'+
      'transparent 25%,'+
      'transparent 50%,'+
      'rgba(255, 255, 255, .2) 50%,'+
      'rgba(255, 255, 255, .2) 75%,'+
      'transparent 75%,'+
      'transparent)',

  };

  return <div style={div_style} >
    <span style={span_style} />
    <div style={candy_style} />
  </div>;
};

export default RiverProgress;
