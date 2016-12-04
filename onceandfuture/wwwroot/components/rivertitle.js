var React = require('react'); // N.B. Still need this because JSX.
import {
  COLUMNSPACER,
  RIVER_TITLE_BACKGROUND_COLOR,
  RIVER_TITLE_FONT_SIZE,
  BUTTON_STYLE,
} from './style';

const RiverSettingsButton = ({river, onShowSettings, onHideSettings}) => {
  const style = Object.assign({}, BUTTON_STYLE, {
    paddingTop: 6,
  });
  const is_settings = (river.modal || {}).kind === 'settings';
  const icon = is_settings ? 'fa-chevron-up' : 'fa-gear';
  const onClick = is_settings ? onHideSettings : onShowSettings;
  return <i className={'fa ' + icon} style={style} onClick={onClick} />;
}

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

  return <i className='fa fa-bars' draggable="true" style={style} onDragStart={onDrag} />;
}

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
