var React = require('react');
import {
  SIZE_BUTTON_HEIGHT,
  SIZE_BUTTON_WIDTH,
  SIZE_BUTTON_PADDING,
  SIZE_BUTTON_FONT,
} from './style';
import Tooltip from './tooltip';

const IconButton = ({tip, tipPosition, icon, onClick}) => {
  const style = {
    width: SIZE_BUTTON_WIDTH,
    height: SIZE_BUTTON_HEIGHT,
    fontSize: SIZE_BUTTON_FONT,
    padding: SIZE_BUTTON_PADDING,
    cursor: 'pointer',
  };
  const className = "fa " + icon;

  return <div style={style}>
    <Tooltip position={tipPosition} tip={tip}>
      <i onClick={onClick} className={className} />
    </Tooltip>
  </div>;
}

export default IconButton;