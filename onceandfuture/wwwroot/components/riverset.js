var React = require('react');
import { connect } from 'react-redux';
import { accountSettingsHide, accountSettingsShow, addRiver, refreshAllFeeds,  } from '../actions';
import {
  APP_BACKGROUND_COLOR,
  APP_TEXT_COLOR,
  BUTTON_STYLE,
  COLUMNSPACER,
  COLUMNWIDTH,
  RIVER_TITLE_FONT_SIZE,
  RIVER_TITLE_BACKGROUND_COLOR,
} from './style';
import AccountSettings from './accountsettings';
import RiverColumn from './rivercolumn';
import RiverProgress from './riverprogress';
import RiverSetBalloon from './riversetballoon';
import Tooltip from './tooltip';

const TITLE_HEIGHT = 33; // <div>"Rivers"..."refresh"</div>

const RiverSetBar = ({title, loading, load_progress, onRefresh, onSettingsClick}) => {
  const div_style = {
    backgroundColor: RIVER_TITLE_BACKGROUND_COLOR,
    height: TITLE_HEIGHT,
  };
  const head_style = {
    fontSize: RIVER_TITLE_FONT_SIZE,
    display: 'inline-block',
    paddingLeft: COLUMNSPACER,
    fontWeight: 'bold',
    paddingTop: 3,
    position: 'relative',
  };

  const refresh_color = loading ? RIVER_TITLE_BACKGROUND_COLOR : APP_TEXT_COLOR;
  const onClick = loading ? () => { } : onRefresh;

  const refresh_style = {
    display: 'inline-block',
    float: 'right',
    color: refresh_color,
    verticalAlign: 'middle',
  };

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
      <div style={head_style}>{title}</div>
      <div style={refresh_style} onClick={onClick} >
        <Tooltip position="bottomleft" tip="Refresh all feeds.">
          <i style={BUTTON_STYLE} onClick={onClick} className="fa fa-refresh" />
        </Tooltip>
      </div>
      <div style={refresh_style} onClick={onSettingsClick}>
        <Tooltip position="bottomleft" tip="View account settings.">
          <i style={BUTTON_STYLE} className="fa fa-user" />
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

export const AddRiverButton = ({onAddRiver}) => {
  const add_button_style = {
    textAlign: 'center',
    fontSize: 'xx-large',
    marginTop: 13,
    cursor: 'pointer',
  };

  return <div style={add_button_style} onClick={onAddRiver}>
      <i className="fa fa-plus-square" />
  </div>;
};

export const RiverSetBase = ({
  user,
  rivers,
  loading,
  load_progress,
  show_settings,
  onRefresh,
  onAddRiver,
  onHideSettings,
  onShowSettings,
}) => {
  const TOTAL_SPACING = COLUMNSPACER * rivers.length;
  const TOTAL_COLUMNS = COLUMNWIDTH * rivers.length;
  const TOP_BAR_HEIGHT = 43;

  const style = {
    position: 'relative',
    height: '100%',
  };
  const top_bar_style = {
    position: 'fixed',
    top: 0, left: 0, width: '100%',
    zIndex: 10,
    height: TOP_BAR_HEIGHT,
  };
  const column_set_style = {
    padding: 10,
    position: 'relative',
    width: TOTAL_SPACING + TOTAL_COLUMNS,
    height: '100%',
  };
  const column_style = {
    width: COLUMNWIDTH,
    position: 'absolute',
    top: 0,
    marginTop: TOP_BAR_HEIGHT + COLUMNSPACER,
    bottom: COLUMNSPACER,
  };
  const add_river_style = Object.assign({}, column_style, {
    left: rivers.length * (COLUMNWIDTH + COLUMNSPACER),
    width: COLUMNWIDTH / 6,
  });


  let accountSettings = <span />;
  let onSettingsClick = onShowSettings;
  if (show_settings) {
    accountSettings = <AccountSettings />;
    onSettingsClick = onHideSettings;
  }

  return (
    <div style={style}>
      <div key='river_set' style={column_set_style}>
      {
        rivers.map((r, index) => {
          const c_style = Object.assign({}, column_style, {
            left: index * (COLUMNWIDTH + COLUMNSPACER) + COLUMNSPACER,
          });
          return <div style={c_style} key={r.name}>
            <RiverColumn index={index} />
          </div>;
        })
      }
      <div style={add_river_style}>
        <AddRiverButton onAddRiver={() => onAddRiver(user)} />
      </div>
      </div>
      <div style={top_bar_style}>
        <RiverSetBar
          title='Rivers'
          loading={loading}
          load_progress={load_progress}
          onRefresh={() => onRefresh(user)}
          onSettingsClick={onSettingsClick}
          />
      </div>
      {accountSettings}
    </div>
  );
};

// VisibleRiverSet
//
const vrs_mapStateToProps = (state) => {
  return {
    user: state.user,
    rivers: state.rivers,
    loading: state.loading,
    load_progress: state.load_progress,
    show_settings: state.account_settings.visible,
  };
};
const vrs_mapDispatchToProps = (dispatch) => {
  return {
    onRefresh: function refreshIt (user) { dispatch(refreshAllFeeds(user)); },
    onAddRiver: function addIt (user) { dispatch(addRiver(user)); },
    onHideSettings: function() { dispatch(accountSettingsHide()); },
    onShowSettings: function() { dispatch(accountSettingsShow()); },
  };
};
const RiverSet = connect(
  vrs_mapStateToProps,
  vrs_mapDispatchToProps
)(
  RiverSetBase
);

export default RiverSet;
