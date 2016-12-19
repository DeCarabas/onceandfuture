import React from 'react';
import {
  RIVER_TITLE_BACKGROUND_COLOR,

  SIZE_RIVER_TITLE_FONT,
  SIZE_RIVER_TITLE_PADDING_HORIZONTAL,
  SIZE_RIVER_TITLE_PADDING_VERTICAL,
  SIZE_RIVER_TITLE_HEIGHT,
} from './style';
import IconButton from './iconbutton';

const RiverSettingsButton = ({river, onShowSettings, onHideSettings}) => {
  const style = {
    position: 'absolute',
    right: 0, top: 0,
  };
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

  return <div style={style}>
    <IconButton tip={tip} icon={icon} onClick={onClick} />
  </div>;
};

const RiverDragHandle = ({river}) => {
  const style = {
    position: 'absolute',
    left: 0, top: 0,
  };

  const onDrag = (ev) => {
    ev.dataTransfer.setData("river", river.id);
    const draggo = ev.target.parentNode.parentNode; //.parentNode; but too slow.
    ev.dataTransfer.setDragImage(draggo, 0, 0);
  };

  return <div style={style} draggable="true" onDragStart={onDrag}>
    <IconButton
      cursor='move'
      tip='Drag this onto another column to re-order it.'
      icon='fa-bars'
    />
  </div>;
};

const RiverTitle = ({river, onShowSettings, onHideSettings}) => {
  const divStyle = {
    height: SIZE_RIVER_TITLE_HEIGHT,
    width: '100%',

    backgroundColor: RIVER_TITLE_BACKGROUND_COLOR,
  };

  const style = {
    position: 'absolute',
    left: SIZE_RIVER_TITLE_PADDING_HORIZONTAL,
    height: '100%',
    paddingTop: SIZE_RIVER_TITLE_PADDING_VERTICAL,
    paddingBottom: SIZE_RIVER_TITLE_PADDING_VERTICAL,
    marginTop: 0,
    fontSize: SIZE_RIVER_TITLE_FONT,
  };

  return <div style={divStyle}>
    <RiverSettingsButton
      river={river}
      onShowSettings={onShowSettings}
      onHideSettings={onHideSettings}
    />
    <RiverDragHandle river={river} />
    <h1 style={style}>{river.name}</h1>
  </div>;
};

export default RiverTitle;
