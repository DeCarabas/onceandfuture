var React = require('react');
import {
  SIZE_ANNOUNCER_FONT,
  SIZE_ANNOUNCER_HEIGHT,
  SIZE_ANNOUNCER_PADDING_VERTICAL,
  SIZE_BANNER_HEIGHT,
  SIZE_BANNER_TITLE_FONT,
  SIZE_BANNER_TITLE_PADDING_HORIZONTAL,
  SIZE_BANNER_TITLE_PADDING_VERTICAL,
  SIZE_SPACER_WIDTH,

  APP_BACKGROUND_COLOR,
  APP_TEXT_COLOR,
  RIVER_TITLE_FONT_SIZE,
  RIVER_TITLE_BACKGROUND_COLOR,
} from './style';
import IconButton from './iconbutton';
import RiverProgress from './riverprogress';
import RiverSetBalloon from './riversetballoon';
import UserMenu from './usermenu';

const BannerTitle = ({title}) => {
  const head_style = {
    position: 'absolute',
    top: 0, left: 0,
    height: SIZE_BANNER_HEIGHT,
    paddingTop: SIZE_BANNER_TITLE_PADDING_VERTICAL,
    paddingBottom: SIZE_BANNER_TITLE_PADDING_VERTICAL,
    paddingLeft: SIZE_BANNER_TITLE_PADDING_HORIZONTAL,
    fontSize: SIZE_BANNER_TITLE_FONT,

    display: 'inline-block',
    fontWeight: 'bold',
  };

  return <div style={head_style}>{title}</div>;
}

const Announcer = ({message}) => {
  const announcer_style = {
    position: 'absolute',
    top: 0,
    width: '100%',
    height: SIZE_ANNOUNCER_HEIGHT,
    fontSize: SIZE_ANNOUNCER_FONT,
    paddingTop: SIZE_ANNOUNCER_PADDING_VERTICAL,
    paddingBottom: SIZE_ANNOUNCER_PADDING_VERTICAL,

    display: 'inline-block',
    textAlign: 'center',
  };

  return <div style={announcer_style}><i>{message}</i></div>;
};

const RefreshFeedsButton = ({onRefresh, loading}) => {
  const onClick = loading ? () => { } : onRefresh;
  const style = {
    color: loading ? RIVER_TITLE_BACKGROUND_COLOR : APP_TEXT_COLOR
  };

  return <div style={style}>
    <IconButton onClick={onClick} icon="fa-refresh" tip="Refresh all feeds." tipPosition="bottomleft" />
  </div>;
}

const AppBanner = ({title, loading, load_progress, onRefresh}) => {
  const div_style = {
    backgroundColor: RIVER_TITLE_BACKGROUND_COLOR,
    height: SIZE_BANNER_HEIGHT,
  };

  const title_button_style = {
    display: 'inline-block',
    float: 'right',
  };

  return <div>
    <div style={div_style}>
      <Announcer message={load_progress.message} />
      <BannerTitle title={title} />
      <div style={title_button_style}>
        <UserMenu />
      </div>
      <div style={title_button_style}>
        <RefreshFeedsButton loading={loading} onRefresh={onRefresh} />
      </div>
    </div>
    <RiverProgress
      progress={load_progress.percent / 100}
      backgroundColor={APP_BACKGROUND_COLOR}
    />
    <RiverSetBalloon />
  </div>;
};

export default AppBanner;