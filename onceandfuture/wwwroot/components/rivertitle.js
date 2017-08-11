import React from 'react';
import {
  COLOR_VERY_DARK,

  RIVER_TITLE_BACKGROUND_COLOR,

  SIZE_RIVER_TITLE_FONT,
  SIZE_RIVER_TITLE_HEIGHT,
} from './style';
import IconButton from './iconbutton';

const RiverSettingsButton = ({ river, onShowSettings, onHideSettings }) => {
  const modal_kind = (river.modal || {}).kind;
  const is_settings = modal_kind === 'settings' || modal_kind === 'ambiguous';

  var icon, onClick, tip;
  if (is_settings) {
    icon = '/up-chevron.opt.svg';
    onClick = onHideSettings;
    tip = 'Close the settings panel.';
  } else {
    icon = '/gear.opt.svg';
    onClick = onShowSettings;
    tip = 'Show the settings panel for this feed.';
  }

  return <IconButton tip={tip} icon={icon} onClick={onClick} />;
};

const RiverDragHandle = ({ river }) => {
  const onDrag = (ev) => {
    ev.dataTransfer.setData("river", river.id);
    const draggo = ev.target.parentNode.parentNode; //.parentNode; but too slow.
    ev.dataTransfer.setDragImage(draggo, 0, 0);
  };

  return <div draggable="true" onDragStart={onDrag}>
    <IconButton
      cursor='move'
      tip='Drag this onto another column to re-order it.'
      icon='/bars.opt.svg'
    />
  </div>;
};

const RiverTitle = ({ river, onShowSettings, onHideSettings }) =>
  <div style={{
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    height: SIZE_RIVER_TITLE_HEIGHT,
    backgroundColor: RIVER_TITLE_BACKGROUND_COLOR,
  }}>
    <div style={{ flex: '0 0 auto' }}>
      <RiverDragHandle river={river} />
    </div>
    <div style={{ flex: '0 0 auto' }}>
      <h1 style={{ fontSize: SIZE_RIVER_TITLE_FONT }}>{river.name}</h1>
    </div>
    <div style={{ flex: '0 0 auto' }}>
      <RiverSettingsButton
        river={river}
        onShowSettings={onShowSettings}
        onHideSettings={onHideSettings}
      />
    </div>
  </div>;

export default RiverTitle;
