var React = require('react');
import {
  SIZE_BANNER_HEIGHT,
  SIZE_SPACER_WIDTH,

  APP_BACKGROUND_COLOR,
  APP_TEXT_COLOR,
  BUTTON_STYLE,
  RIVER_TITLE_FONT_SIZE,
  RIVER_TITLE_BACKGROUND_COLOR,
} from './style';
import RiverProgress from './riverprogress';
import RiverSetBalloon from './riversetballoon';
import Tooltip from './tooltip';
import UserMenu from './usermenu';

const BannerTitle = ({title}) => {
  const head_style = {
    fontSize: RIVER_TITLE_FONT_SIZE,
    display: 'inline-block',
    paddingLeft: SIZE_SPACER_WIDTH,
    fontWeight: 'bold',
    paddingTop: 3,
    position: 'relative',
  };

  return <div style={head_style}>{title}</div>;
}

const AppBanner = ({title, loading, load_progress, onRefresh, onSettingsClick}) => {
  const div_style = {
    backgroundColor: RIVER_TITLE_BACKGROUND_COLOR,
    height: SIZE_BANNER_HEIGHT,
  };

  const refresh_color = loading ? RIVER_TITLE_BACKGROUND_COLOR : APP_TEXT_COLOR;
  const onClick = loading ? () => { } : onRefresh;

  const title_button_style = {
    display: 'inline-block',
    float: 'right',
    verticalAlign: 'middle',
    textAlign: 'center',
  };
  const refresh_style = Object.assign({}, title_button_style, {
    color: refresh_color,
  });

  const announcer_style = {
    display: 'inline-block',
    textAlign: 'center',
    width: '100%',
    verticalAlign: 'middle',
    height: 'auto',
    position: 'relative',
    top: -20,
  };

  return <div>
    <div style={div_style}>
      <BannerTitle title={title} />
      <div style={title_button_style}>
        <UserMenu />
      </div>
      <div style={refresh_style} onClick={onClick} >
        <Tooltip position="bottomleft" tip="Refresh all feeds.">
          <i style={BUTTON_STYLE} onClick={onClick} className="fa fa-refresh" />
        </Tooltip>
      </div>
      <div style={announcer_style}><i>{load_progress.message}</i></div>
    </div>
    <RiverProgress
      progress={load_progress.percent / 100}
      backgroundColor={APP_BACKGROUND_COLOR}
    />
    <RiverSetBalloon />
  </div>;
};

export default AppBanner;