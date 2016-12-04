var React = require('react'); // N.B. Still need this because JSX.
import {
  COLUMNSPACER,
  RIVER_TITLE_BACKGROUND_COLOR,
  RIVER_TITLE_FONT_SIZE,
  BUTTON_STYLE,
} from './style';
import Tooltip from './tooltip';

const RiverSettingsButton = ({river, onShowSettings, onHideSettings}) => {
  const style = Object.assign({}, BUTTON_STYLE, {
    paddingTop: 6,
  });
  const is_settings = (river.modal || {}).kind === 'settings';

  var icon, onClick, tip;
  if (is_settings) {
    icon = 'fa-chevron-up';
    onClick = onHideSettings;
    tip = 'Close the settings panel.';
  } else {
    icon = 'fa-gear';
    onClick = onShowSettings;
    tip = 'Show the settings panel for this feed.';
  }

  return <span style={style}>
    <Tooltip tip={tip} position='right'>
      <i className={'fa ' + icon} onClick={onClick} />
    </Tooltip>
  </span>;
};

const RiverDragHandle = ({river}) => {
  const style = Object.assign({}, BUTTON_STYLE, {
    paddingTop: 6,
    cursor: 'move',
    float: null,
  });

  const onDrag = (ev) => {
    ev.dataTransfer.setData("river", river.id);
    const draggo = ev.target.parentNode.parentNode; //.parentNode; but too slow.
    ev.dataTransfer.setDragImage(draggo, 0, 0);
  };

  return <span style={style} draggable="true" onDragStart={onDrag}>
    <Tooltip tip='Drag this onto another column to re-order it.' position='right'>
      <i className='fa fa-bars' />
    </Tooltip>
  </span>;
};

const RiverTitle = ({river, onShowSettings, onHideSettings}) => {
  const divStyle = {
    backgroundColor: RIVER_TITLE_BACKGROUND_COLOR,
    verticalAlign: 'middle',
    userSelect: 'none',
    draggable: 'true',
  };
  const style = {
    paddingLeft: COLUMNSPACER,
    fontSize: RIVER_TITLE_FONT_SIZE,
    marginBottom: 0,
    userSelect: 'none',
    draggable: 'true',
  };

  return <div style={divStyle}>
    <RiverSettingsButton
      river={river}
      onShowSettings={onShowSettings}
      onHideSettings={onHideSettings}
      />
    <h1 style={style}><RiverDragHandle river={river} /> {river.name}</h1>
  </div>;
};

export default RiverTitle;
