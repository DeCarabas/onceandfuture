var React = require('react'); // N.B. Still need this because JSX.
import {
  COLUMNWIDTH,
  COLUMNSPACER,
  ICON_FONT_SIZE,
  RIVER_TITLE_BACKGROUND_COLOR,
  RIVER_TITLE_FONT_SIZE,
  BUTTON_STYLE,
} from './style'

const RiverSettingsButton = ({river, onShowSettings, onHideSettings}) => {
  const is_settings = (river.modal || {}).kind === 'settings';
  const icon = is_settings ? 'fa-chevron-up' : 'fa-gear';
  const onClick = is_settings ? onHideSettings : onShowSettings;
  return <i className={'fa ' + icon} style={BUTTON_STYLE} onClick={onClick} />
}

const RiverTitle = ({river, onShowSettings, onHideSettings}) => {
  const divStyle = {
    backgroundColor: RIVER_TITLE_BACKGROUND_COLOR,
  }
  const style = {
    paddingLeft: COLUMNSPACER,
    fontSize: RIVER_TITLE_FONT_SIZE,
    marginBottom: 0,
  }

  return <div style={divStyle}>
    <RiverSettingsButton
      river={river}
      onShowSettings={onShowSettings}
      onHideSettings={onHideSettings}
      />
    <h1 style={style}>{river.name}</h1>
  </div>;
};

export default RiverTitle;
