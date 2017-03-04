import React from 'react';
import {
  COLOR_VERY_DARK,

  RIVER_TITLE_BACKGROUND_COLOR,

  SIZE_RIVER_TITLE_FONT,
  SIZE_RIVER_TITLE_HEIGHT,
} from './style';
import IconButton from './iconbutton';

const RiverSettingsButton = ({river, onShowSettings, onHideSettings}) => {
  // N.B. you might be tempted to style this with transform: translate() to
  // match the style of the title in the center; but this puts the tooltip in
  // its own stacking context and it gets sorted under all the other elements.
  //
  const style = {
    position: 'absolute',
    right: 0, top: 0,
  };
  const modal_kind = (river.modal || {}).kind;
  const is_settings = modal_kind === 'settings' || modal_kind === 'ambiguous';

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
  // N.B. you might be tempted to style this with transform: translate() to
  // match the style of the title in the center; but this puts the tooltip in
  // its own stacking context and it gets sorted under all the other elements.
  //
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
    //width: SIZE_COLUMN_WIDTH,
    position: 'absolute',
    left: 0,
    right: 1, // ::shrug::
    borderTop: '1px solid ' + COLOR_VERY_DARK,

    backgroundColor: RIVER_TITLE_BACKGROUND_COLOR,
  };

  const style = {
    position: 'absolute',
    fontSize: SIZE_RIVER_TITLE_FONT,
    marginTop: 0,
    left: '50%', top: '50%',
    transform: 'translateX(-50%) translateY(-50%)',
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
